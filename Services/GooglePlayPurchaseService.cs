using Google.Apis.AndroidPublisher.v3;
using Google.Apis.AndroidPublisher.v3.Data;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Services;
using FitnessTracker.API.DTOs;
using FitnessTracker.API.Models;
using FitnessTracker.API.Repositories;
using FitnessTracker.API.Data; 
using Microsoft.EntityFrameworkCore; 

namespace FitnessTracker.API.Services
{
    public interface IGooglePlayPurchaseService
    {
        Task<PurchaseVerificationResponse> VerifyPurchaseAsync(string userId, VerifyGooglePurchaseRequest request);
        Task<RestorePurchasesResponse> RestorePurchasesAsync(string userId, List<string> purchaseTokens);
        Task<SubscriptionStatusResponse> GetSubscriptionStatusAsync(string userId);
    }

    public class GooglePlayPurchaseService : IGooglePlayPurchaseService
    {
        private readonly IConfiguration _configuration;
        private readonly ILwCoinService _lwCoinService;
        private readonly IUserRepository _userRepository;
        private readonly ApplicationDbContext _context;
        private readonly ILogger<GooglePlayPurchaseService> _logger;
        private AndroidPublisherService? _androidPublisher;

        private const string PACKAGE_NAME = "dev.tfox.lw"; 
        private readonly Dictionary<string, (int coins, int days, decimal price)> _productMapping = new()
        {
            ["lw_subscription_weekly"] = (50, 7, 0.99m),
            ["lw_subscription_biweekly"] = (100, 14, 1.99m),
            ["lw_subscription_monthly_basic"] = (100, 30, 2.99m),
            ["lw_subscription_monthly_standard"] = (200, 30, 3.99m),
            ["lw_subscription_monthly_premium"] = (500, 30, 7.99m),
            ["lw_subscription_unlimited"] = (9999, 30, 8.99m),

            ["lw_coins_50"] = (50, 0, 0.99m),
            ["lw_coins_100"] = (100, 0, 1.99m),
            ["lw_coins_200"] = (200, 0, 3.99m),
            ["lw_coins_500"] = (500, 0, 8.99m)
        };

        public GooglePlayPurchaseService(
            IConfiguration configuration,
            ILwCoinService lwCoinService,
            IUserRepository userRepository,
            ApplicationDbContext context,
            ILogger<GooglePlayPurchaseService> logger)
        {
            _configuration = configuration;
            _lwCoinService = lwCoinService;
            _userRepository = userRepository;
            _context = context;
            _logger = logger;
        }

        private async Task<AndroidPublisherService> GetPublisherServiceAsync()
        {
            if (_androidPublisher != null)
                return _androidPublisher;

            try
            {
                var serviceAccountPath = _configuration["GooglePlay:ServiceAccountPath"] ?? "google-play-service-account.json";

                GoogleCredential credential;
                using (var stream = new FileStream(serviceAccountPath, FileMode.Open, FileAccess.Read))
                {
                    credential = GoogleCredential.FromStream(stream)
                        .CreateScoped(AndroidPublisherService.Scope.Androidpublisher);
                }

                _androidPublisher = new AndroidPublisherService(new BaseClientService.Initializer
                {
                    HttpClientInitializer = credential,
                    ApplicationName = "FitnessTracker API"
                });

                return _androidPublisher;
            }
            catch (Exception ex)
            {
                _logger.LogError($"❌ Failed to initialize Google Play Publisher: {ex.Message}");
                throw;
            }
        }

        public async Task<PurchaseVerificationResponse> VerifyPurchaseAsync(string userId, VerifyGooglePurchaseRequest request)
        {
            try
            {
                _logger.LogInformation($"🔍 Verifying Google Play purchase for user {userId}, product: {request.ProductId}");

                var existingVerification = await _context.Set<PurchaseVerification>()
                    .FirstOrDefaultAsync(v => v.UserId == userId &&
                                            v.PurchaseToken == request.PurchaseToken &&
                                            v.VerificationStatus == "verified");

                if (existingVerification != null)
                {
                    _logger.LogWarning($"⚠️ Purchase already verified: {request.PurchaseToken}");
                    return new PurchaseVerificationResponse
                    {
                        Success = true,
                        VerificationId = existingVerification.Id,
                        Status = "already_verified",
                        Message = "This purchase has already been processed"
                    };
                }

                var service = await GetPublisherServiceAsync();

                var verification = new PurchaseVerification
                {
                    UserId = userId,
                    Platform = "google",
                    PurchaseToken = request.PurchaseToken,
                    ProductId = request.ProductId,
                    PackageType = request.PackageType,
                    VerificationStatus = "pending"
                };

                _context.Set<PurchaseVerification>().Add(verification);
                await _context.SaveChangesAsync();

                try
                {
                    if (request.PackageType == "subscription")
                    {
                        var subscriptionPurchase = await service.Purchases.Subscriptions
                            .Get(PACKAGE_NAME, request.ProductId, request.PurchaseToken)
                            .ExecuteAsync();

                        if (subscriptionPurchase == null)
                        {
                            throw new Exception("Subscription not found");
                        }

                        if (subscriptionPurchase.PaymentState != 1) 
                        {
                            verification.VerificationStatus = "payment_pending";
                            verification.VerificationError = "Payment not yet received";
                            await _context.SaveChangesAsync();

                            return new PurchaseVerificationResponse
                            {
                                Success = false,
                                VerificationId = verification.Id,
                                Status = "payment_pending",
                                Message = "Payment is still being processed"
                            };
                        }

                        if (subscriptionPurchase.UserCancellationTimeMillis.HasValue)
                        {
                            verification.VerificationStatus = "cancelled";
                            verification.VerificationError = "Subscription was cancelled";
                            await _context.SaveChangesAsync();

                            return new PurchaseVerificationResponse
                            {
                                Success = false,
                                VerificationId = verification.Id,
                                Status = "cancelled",
                                Message = "This subscription has been cancelled"
                            };
                        }

                        if (!_productMapping.TryGetValue(request.ProductId, out var productInfo))
                        {
                            _logger.LogWarning($"⚠️ Unknown product ID: {request.ProductId}");
                            productInfo = (100, 30, 2.99m); 
                        }

                        var expiryTimeMillis = subscriptionPurchase.ExpiryTimeMillis;
                        DateTime? expiresAt = null;
                        if (expiryTimeMillis.HasValue)
                        {
                            expiresAt = DateTimeOffset.FromUnixTimeMilliseconds(expiryTimeMillis.Value).UtcDateTime;
                        }

                        verification.VerificationStatus = "verified";
                        verification.VerifiedAt = DateTime.UtcNow;
                        verification.ExpiresAt = expiresAt;
                        verification.Price = productInfo.price;
                        verification.CoinsAmount = productInfo.coins;
                        verification.DurationDays = productInfo.days;

                        await _context.SaveChangesAsync();

                        bool coinsAdded = false;
                        if (productInfo.days > 0)
                        {
                            coinsAdded = await _lwCoinService.PurchaseSubscriptionCoinsAsync(
                                userId, productInfo.coins, productInfo.days, productInfo.price);
                        }
                        else
                        {
                            coinsAdded = await _lwCoinService.AddLwCoinsAsync(
                                userId, productInfo.coins, "purchase", $"Google Play purchase: {request.ProductId}");
                        }

                        _logger.LogInformation($"✅ Google Play subscription verified: {request.ProductId}, coins added: {coinsAdded}");

                        return new PurchaseVerificationResponse
                        {
                            Success = true,
                            VerificationId = verification.Id,
                            Status = "verified",
                            Message = "Purchase verified successfully",
                            CoinsAdded = productInfo.coins,
                            ExpiresAt = expiresAt,
                            IsSubscription = true,
                            NewBalance = await GetUserBalance(userId)
                        };
                    }
                    else
                    {
                        var productPurchase = await service.Purchases.Products
                            .Get(PACKAGE_NAME, request.ProductId, request.PurchaseToken)
                            .ExecuteAsync();

                        if (productPurchase == null)
                        {
                            throw new Exception("Purchase not found");
                        }

                        if (productPurchase.PurchaseState != 0)
                        {
                            verification.VerificationStatus = "invalid";
                            verification.VerificationError = "Purchase is not in valid state";
                            await _context.SaveChangesAsync();

                            return new PurchaseVerificationResponse
                            {
                                Success = false,
                                VerificationId = verification.Id,
                                Status = "invalid",
                                Message = "This purchase is not valid"
                            };
                        }

                        if (productPurchase.ConsumptionState == 0) 
                        {
                            await service.Purchases.Products
                                .Consume(PACKAGE_NAME, request.ProductId, request.PurchaseToken)
                                .ExecuteAsync();
                        }

                        if (!_productMapping.TryGetValue(request.ProductId, out var productInfo))
                        {
                            _logger.LogWarning($"⚠️ Unknown product ID: {request.ProductId}");
                            productInfo = (100, 0, 1.99m); 
                        }

                        verification.VerificationStatus = "verified";
                        verification.VerifiedAt = DateTime.UtcNow;
                        verification.Price = productInfo.price;
                        verification.CoinsAmount = productInfo.coins;
                        verification.DurationDays = 0;

                        await _context.SaveChangesAsync();

                        var coinsAdded = await _lwCoinService.AddLwCoinsAsync(
                            userId, productInfo.coins, "purchase", $"Google Play purchase: {request.ProductId}");

                        _logger.LogInformation($"✅ Google Play product verified: {request.ProductId}, coins: {productInfo.coins}");

                        return new PurchaseVerificationResponse
                        {
                            Success = true,
                            VerificationId = verification.Id,
                            Status = "verified",
                            Message = "Purchase verified successfully",
                            CoinsAdded = productInfo.coins,
                            IsSubscription = false,
                            NewBalance = await GetUserBalance(userId)
                        };
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError($"❌ Verification failed: {ex.Message}");

                    verification.VerificationStatus = "failed";
                    verification.VerificationError = ex.Message;
                    await _context.SaveChangesAsync();

                    return new PurchaseVerificationResponse
                    {
                        Success = false,
                        VerificationId = verification.Id,
                        Status = "failed",
                        Message = "Verification failed",
                        ErrorDetails = ex.Message
                    };
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"❌ Google Play verification error: {ex.Message}");
                return new PurchaseVerificationResponse
                {
                    Success = false,
                    Status = "error",
                    Message = "Internal error during verification",
                    ErrorDetails = ex.Message
                };
            }
        }

        public async Task<RestorePurchasesResponse> RestorePurchasesAsync(string userId, List<string> purchaseTokens)
        {
            var response = new RestorePurchasesResponse
            {
                Success = true,
                RestoredPurchases = new List<RestoredPurchaseInfo>()
            };

            foreach (var token in purchaseTokens)
            {
                try
                {
                    var service = await GetPublisherServiceAsync();

                    foreach (var product in _productMapping.Keys)
                    {
                        try
                        {
                            if (product.Contains("subscription"))
                            {
                                var subscription = await service.Purchases.Subscriptions
                                    .Get(PACKAGE_NAME, product, token)
                                    .ExecuteAsync();

                                if (subscription != null && subscription.PaymentState == 1)
                                {
                                    var productInfo = _productMapping[product];

                                    response.RestoredPurchases.Add(new RestoredPurchaseInfo
                                    {
                                        ProductId = product,
                                        CoinsAmount = productInfo.coins,
                                        ExpiresAt = subscription.ExpiryTimeMillis.HasValue
                                            ? DateTimeOffset.FromUnixTimeMilliseconds(subscription.ExpiryTimeMillis.Value).UtcDateTime
                                            : null,
                                        IsSubscription = true
                                    });

                                    response.RestoredCount++;
                                    response.TotalCoinsRestored += productInfo.coins;
                                    break;
                                }
                            }
                        }
                        catch
                        {

                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError($"❌ Failed to restore token {token}: {ex.Message}");
                }
            }

            response.Message = $"Restored {response.RestoredCount} purchases";
            return response;
        }

        public async Task<SubscriptionStatusResponse> GetSubscriptionStatusAsync(string userId)
        {
            try
            {
                var activeVerification = await _context.Set<PurchaseVerification>()
                    .Where(v => v.UserId == userId &&
                               v.Platform == "google" &&
                               v.VerificationStatus == "verified" &&
                               v.ExpiresAt != null &&
                               v.ExpiresAt > DateTime.UtcNow)
                    .OrderByDescending(v => v.ExpiresAt)
                    .FirstOrDefaultAsync();

                if (activeVerification == null)
                {
                    return new SubscriptionStatusResponse
                    {
                        HasActiveSubscription = false,
                        Platform = "google"
                    };
                }

                return new SubscriptionStatusResponse
                {
                    HasActiveSubscription = true,
                    ProductId = activeVerification.ProductId,
                    ExpiresAt = activeVerification.ExpiresAt,
                    DaysRemaining = activeVerification.ExpiresAt.HasValue
                        ? (int)(activeVerification.ExpiresAt.Value - DateTime.UtcNow).TotalDays
                        : 0,
                    IsAutoRenewing = true, 
                    Platform = "google"
                };
            }
            catch (Exception ex)
            {
                _logger.LogError($"❌ Error getting subscription status: {ex.Message}");
                return new SubscriptionStatusResponse
                {
                    HasActiveSubscription = false,
                    Platform = "google"
                };
            }
        }

        private async Task<int> GetUserBalance(string userId)
        {
            var balance = await _lwCoinService.GetUserLwCoinBalanceAsync(userId);
            return balance.Balance;
        }
    }
}
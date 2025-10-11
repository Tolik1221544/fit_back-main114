using System.IdentityModel.Tokens.Jwt;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.IdentityModel.Tokens;
using FitnessTracker.API.DTOs;
using FitnessTracker.API.Models;
using FitnessTracker.API.Repositories;
using FitnessTracker.API.Data;
using Microsoft.EntityFrameworkCore;

namespace FitnessTracker.API.Services
{
    public interface IApplePurchaseService
    {
        Task<PurchaseVerificationResponse> VerifyPurchaseAsync(string userId, VerifyApplePurchaseRequest request);
        Task<RestorePurchasesResponse> RestorePurchasesAsync(string userId, string receiptData);
        Task<SubscriptionStatusResponse> GetSubscriptionStatusAsync(string userId);
        Task<bool> ValidateTransactionAsync(string signedTransactionInfo);
    }

    public class ApplePurchaseService : IApplePurchaseService
    {
        private readonly IConfiguration _configuration;
        private readonly ILwCoinService _lwCoinService;
        private readonly IUserRepository _userRepository;
        private readonly ApplicationDbContext _context;
        private readonly ILogger<ApplePurchaseService> _logger;
        private readonly HttpClient _httpClient;

        private const string PRODUCTION_URL = "https://buy.itunes.apple.com/verifyReceipt";
        private const string SANDBOX_URL = "https://sandbox.itunes.apple.com/verifyReceipt";

        private string BundleId => _configuration["Apple:BundleId"] ?? "dev.tfox.lw";
        private string SharedSecret => _configuration["Apple:SharedSecret"] ?? "";
        private bool UseSandbox => _configuration.GetValue<bool>("Apple:UseSandbox", false);

        private readonly Dictionary<string, (int coins, int days, decimal price)> _productMapping = new()
        {
            ["dev.tfox.lw.subscription.weekly"] = (50, 7, 0.99m),
            ["dev.tfox.lw.subscription.biweekly"] = (100, 14, 1.99m),
            ["dev.tfox.lw.subscription.monthly.basic"] = (100, 30, 2.99m),
            ["dev.tfox.lw.subscription.monthly.standard"] = (200, 30, 3.99m),
            ["dev.tfox.lw.subscription.monthly.premium"] = (500, 30, 7.99m),
            ["dev.tfox.lw.subscription.unlimited"] = (9999, 30, 8.99m),

            ["dev.tfox.lw.coins.50"] = (50, 0, 0.99m),
            ["dev.tfox.lw.coins.100"] = (100, 0, 1.99m),
            ["dev.tfox.lw.coins.200"] = (200, 0, 3.99m),
            ["dev.tfox.lw.coins.500"] = (500, 0, 8.99m)
        };

        public ApplePurchaseService(
            IConfiguration configuration,
            ILwCoinService lwCoinService,
            IUserRepository userRepository,
            ApplicationDbContext context,
            ILogger<ApplePurchaseService> logger,
            HttpClient httpClient)
        {
            _configuration = configuration;
            _lwCoinService = lwCoinService;
            _userRepository = userRepository;
            _context = context;
            _logger = logger;
            _httpClient = httpClient;
        }

        public async Task<PurchaseVerificationResponse> VerifyPurchaseAsync(string userId, VerifyApplePurchaseRequest request)
        {
            try
            {
                _logger.LogInformation($"🍎 Verifying Apple purchase for user {userId}, product: {request.ProductId}");

                var existingVerification = await _context.Set<PurchaseVerification>()
                    .FirstOrDefaultAsync(v => v.UserId == userId &&
                                            v.TransactionId == request.TransactionId &&
                                            v.VerificationStatus == "verified");

                if (existingVerification != null)
                {
                    _logger.LogWarning($"⚠️ Purchase already verified: {request.TransactionId}");
                    return new PurchaseVerificationResponse
                    {
                        Success = true,
                        VerificationId = existingVerification.Id,
                        Status = "already_verified",
                        Message = "This purchase has already been processed"
                    };
                }

                var verification = new PurchaseVerification
                {
                    UserId = userId,
                    Platform = "apple",
                    TransactionId = request.TransactionId,
                    ProductId = request.ProductId,
                    PackageType = request.PackageType,
                    VerificationStatus = "pending"
                };

                _context.Set<PurchaseVerification>().Add(verification);
                await _context.SaveChangesAsync();

                try
                {
                    var verifyResult = await VerifyReceiptWithAppleAsync(request.ReceiptData);

                    if (!verifyResult.IsValid)
                    {
                        verification.VerificationStatus = "failed";
                        verification.VerificationError = verifyResult.Error;
                        await _context.SaveChangesAsync();

                        return new PurchaseVerificationResponse
                        {
                            Success = false,
                            VerificationId = verification.Id,
                            Status = "invalid",
                            Message = verifyResult.Error ?? "Receipt validation failed"
                        };
                    }

                    var transaction = verifyResult.Receipt?.InAppPurchases?
                        .FirstOrDefault(p => p.TransactionId == request.TransactionId);

                    if (transaction == null)
                    {
                        verification.VerificationStatus = "not_found";
                        verification.VerificationError = "Transaction not found in receipt";
                        await _context.SaveChangesAsync();

                        return new PurchaseVerificationResponse
                        {
                            Success = false,
                            VerificationId = verification.Id,
                            Status = "not_found",
                            Message = "Transaction not found"
                        };
                    }

                    if (transaction.ProductId != request.ProductId)
                    {
                        _logger.LogWarning($"⚠️ Product ID mismatch: expected {request.ProductId}, got {transaction.ProductId}");
                    }

                    if (!_productMapping.TryGetValue(transaction.ProductId, out var productInfo))
                    {
                        _logger.LogWarning($"⚠️ Unknown product ID: {transaction.ProductId}");
                        productInfo = (100, 0, 1.99m);
                    }

                    DateTime? expiresAt = null;
                    if (request.PackageType == "subscription" && transaction.ExpiresDateMs.HasValue)
                    {
                        expiresAt = DateTimeOffset.FromUnixTimeMilliseconds(transaction.ExpiresDateMs.Value).UtcDateTime;

                        if (expiresAt < DateTime.UtcNow)
                        {
                            verification.VerificationStatus = "expired";
                            verification.VerificationError = "Subscription has expired";
                            await _context.SaveChangesAsync();

                            return new PurchaseVerificationResponse
                            {
                                Success = false,
                                VerificationId = verification.Id,
                                Status = "expired",
                                Message = "This subscription has expired"
                            };
                        }
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
                            userId, productInfo.coins, "purchase", $"Apple purchase: {transaction.ProductId}");
                    }

                    _logger.LogInformation($"✅ Apple purchase verified: {transaction.ProductId}, coins added: {coinsAdded}");

                    return new PurchaseVerificationResponse
                    {
                        Success = true,
                        VerificationId = verification.Id,
                        Status = "verified",
                        Message = "Purchase verified successfully",
                        CoinsAdded = productInfo.coins,
                        ExpiresAt = expiresAt,
                        IsSubscription = request.PackageType == "subscription",
                        NewBalance = await GetUserBalance(userId)
                    };
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
                _logger.LogError($"❌ Apple verification error: {ex.Message}");
                return new PurchaseVerificationResponse
                {
                    Success = false,
                    Status = "error",
                    Message = "Internal error during verification",
                    ErrorDetails = ex.Message
                };
            }
        }

        public async Task<RestorePurchasesResponse> RestorePurchasesAsync(string userId, string receiptData)
        {
            var response = new RestorePurchasesResponse
            {
                Success = true,
                RestoredPurchases = new List<RestoredPurchaseInfo>()
            };

            try
            {
                var verifyResult = await VerifyReceiptWithAppleAsync(receiptData);

                if (!verifyResult.IsValid || verifyResult.Receipt?.InAppPurchases == null)
                {
                    response.Success = false;
                    response.Message = "Failed to verify receipt";
                    return response;
                }

                foreach (var purchase in verifyResult.Receipt.InAppPurchases)
                {
                    if (!_productMapping.TryGetValue(purchase.ProductId, out var productInfo))
                    {
                        continue;
                    }

                    var existingVerification = await _context.Set<PurchaseVerification>()
                        .FirstOrDefaultAsync(v => v.UserId == userId &&
                                                v.TransactionId == purchase.TransactionId &&
                                                v.VerificationStatus == "verified");

                    if (existingVerification == null)
                    {
                        DateTime? expiresAt = null;
                        if (purchase.ExpiresDateMs.HasValue)
                        {
                            expiresAt = DateTimeOffset.FromUnixTimeMilliseconds(purchase.ExpiresDateMs.Value).UtcDateTime;

                            if (expiresAt < DateTime.UtcNow)
                            {
                                continue;
                            }
                        }

                        response.RestoredPurchases.Add(new RestoredPurchaseInfo
                        {
                            ProductId = purchase.ProductId,
                            CoinsAmount = productInfo.coins,
                            ExpiresAt = expiresAt,
                            IsSubscription = productInfo.days > 0
                        });

                        response.RestoredCount++;
                        response.TotalCoinsRestored += productInfo.coins;
                    }
                }

                response.Message = $"Restored {response.RestoredCount} purchases";
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError($"❌ Error restoring purchases: {ex.Message}");
                response.Success = false;
                response.Message = "Error restoring purchases";
                return response;
            }
        }

        public async Task<SubscriptionStatusResponse> GetSubscriptionStatusAsync(string userId)
        {
            try
            {
                var activeVerification = await _context.Set<PurchaseVerification>()
                    .Where(v => v.UserId == userId &&
                               v.Platform == "apple" &&
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
                        Platform = "apple"
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
                    IsAutoRenewing = false,
                    Platform = "apple"
                };
            }
            catch (Exception ex)
            {
                _logger.LogError($"❌ Error getting subscription status: {ex.Message}");
                return new SubscriptionStatusResponse
                {
                    HasActiveSubscription = false,
                    Platform = "apple"
                };
            }
        }

        public Task<bool> ValidateTransactionAsync(string signedTransactionInfo)
        {
            return Task.FromResult(true);
        }

        private async Task<AppleReceiptVerificationResult> VerifyReceiptWithAppleAsync(string receiptData)
        {
            var requestBody = new
            {
                receipt_data = receiptData,
                password = _configuration["Apple:SharedSecret"] ?? "",
                exclude_old_transactions = true
            };

            var json = JsonSerializer.Serialize(requestBody);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync(PRODUCTION_URL, content);
            var responseContent = await response.Content.ReadAsStringAsync();

            var result = JsonSerializer.Deserialize<AppleReceiptResponse>(responseContent,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (result?.Status == 21007)
            {
                response = await _httpClient.PostAsync(SANDBOX_URL, content);
                responseContent = await response.Content.ReadAsStringAsync();
                result = JsonSerializer.Deserialize<AppleReceiptResponse>(responseContent,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            }

            if (result?.Status != 0)
            {
                return new AppleReceiptVerificationResult
                {
                    IsValid = false,
                    Error = $"Apple verification failed with status: {result?.Status}"
                };
            }

            return new AppleReceiptVerificationResult
            {
                IsValid = true,
                Receipt = result?.Receipt
            };
        }

        private async Task<int> GetUserBalance(string userId)
        {
            var balance = await _lwCoinService.GetUserLwCoinBalanceAsync(userId);
            return balance.Balance;
        }
    }

    public class AppleReceiptVerificationResult
    {
        public bool IsValid { get; set; }
        public string? Error { get; set; }
        public AppleReceipt? Receipt { get; set; }
    }

    public class AppleReceiptResponse
    {
        public int Status { get; set; }
        public AppleReceipt? Receipt { get; set; }
    }

    public class AppleReceipt
    {
        public string? BundleId { get; set; }
        public List<AppleInAppPurchase>? InAppPurchases { get; set; }
    }

    public class AppleInAppPurchase
    {
        public string ProductId { get; set; } = "";
        public string TransactionId { get; set; } = "";
        public long? PurchaseDateMs { get; set; }
        public long? ExpiresDateMs { get; set; }
        public int Quantity { get; set; }
    }
}
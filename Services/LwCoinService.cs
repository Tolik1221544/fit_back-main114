using FitnessTracker.API.DTOs;
using FitnessTracker.API.Models;
using FitnessTracker.API.Repositories;
using AutoMapper;

namespace FitnessTracker.API.Services
{
    public class LwCoinService : ILwCoinService
    {
        private readonly ILwCoinRepository _lwCoinRepository;
        private readonly IUserRepository _userRepository;
        private readonly IMapper _mapper;
        private readonly ILogger<LwCoinService> _logger;

        private const decimal PHOTO_FOOD_SCAN_COST = 1.0m;
        private const decimal VOICE_FOOD_SCAN_COST = 1.0m;
        private const decimal TEXT_FOOD_SCAN_COST = 0.0m;
        private const decimal FOOD_CORRECTION_COST = 0.0m;
        private const decimal VOICE_WORKOUT_COST = 1.0m;
        private const decimal TEXT_WORKOUT_COST = 0.0m;
        private const decimal BODY_ANALYSIS_COST = 0.0m;

        private const int REGISTRATION_BONUS = 10;
        private const int REFERRAL_BONUS = 150;
        private const decimal PREMIUM_PRICE = 8.99m;

        public LwCoinService(
            ILwCoinRepository lwCoinRepository,
            IUserRepository userRepository,
            IMapper mapper,
            ILogger<LwCoinService> logger)
        {
            _lwCoinRepository = lwCoinRepository;
            _userRepository = userRepository;
            _mapper = mapper;
            _logger = logger;
        }

        public async Task<bool> SetUserCoinsAsync(string userId, decimal amount, string source = "manual")
        {
            try
            {
                var user = await _userRepository.GetByIdAsync(userId);
                if (user == null) return false;

                var oldBalance = user.FractionalLwCoins;
                user.FractionalLwCoins = (double)amount;
                user.LwCoins = (int)Math.Floor(amount);
                await _userRepository.UpdateAsync(user);

                await CreateTransactionAsync(userId, (int)amount, amount, "manual_set",
                    $"Manual balance set from {oldBalance} to {amount}", "", source);

                _logger.LogInformation($"💰 Manual set coins for user {userId}: {oldBalance} -> {amount}");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError($"❌ Error setting coins: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> AddRegistrationBonusAsync(string userId)
        {
            try
            {
                var user = await _userRepository.GetByIdAsync(userId);
                if (user == null)
                {
                    _logger.LogError($"User {userId} not found for registration bonus");
                    return false;
                }

                var transactions = await _lwCoinRepository.GetUserTransactionsAsync(userId);
                if (transactions.Any(t => t.CoinSource == "registration" || t.Description.Contains("Welcome bonus")))
                {
                    _logger.LogWarning($"User {userId} already has registration bonus");
                    return true; 
                }

                user.FractionalLwCoins += REGISTRATION_BONUS;
                user.LwCoins = (int)Math.Floor(user.FractionalLwCoins);

                await _userRepository.UpdateAsync(user);

                var transaction = new LwCoinTransaction
                {
                    UserId = userId,
                    Amount = REGISTRATION_BONUS,
                    FractionalAmount = REGISTRATION_BONUS,
                    Type = "registration",
                    Description = "Welcome bonus - 10 coins",
                    CoinSource = "registration",
                    FeatureUsed = "",
                    CreatedAt = DateTime.UtcNow
                };

                await _lwCoinRepository.CreateTransactionAsync(transaction);

                _logger.LogInformation($"🎁 Registration bonus successfully added for user {userId}: +{REGISTRATION_BONUS} coins");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError($"❌ Critical error adding registration bonus for {userId}: {ex.Message}");
                return false;
            }
        }

        public async Task<LwCoinBalanceDto> GetUserLwCoinBalanceAsync(string userId)
        {
            var user = await _userRepository.GetByIdAsync(userId);
            if (user == null) throw new ArgumentException("User not found");

            var isPremium = await IsUserPremiumAsync(userId);
            var balanceDetails = await GetDetailedBalanceAsync(userId);
            var premiumExpiresAt = await GetPremiumExpiryAsync(userId);
            var notification = await GeneratePremiumNotificationAsync(userId, isPremium, premiumExpiresAt);

            var balanceDto = new LwCoinBalanceDto
            {
                Balance = (int)Math.Floor(user.FractionalLwCoins),

                DetailedBalance = new
                {
                    Subscription = balanceDetails.SubscriptionCoins,
                    Referral = balanceDetails.ReferralCoins,
                    Bonus = balanceDetails.BonusCoins,
                    Permanent = balanceDetails.PermanentCoins,
                    Registration = balanceDetails.RegistrationCoins,
                    Total = balanceDetails.TotalCoins
                },

                IsPremium = isPremium,
                PremiumExpiresAt = premiumExpiresAt,
                NextRefillDate = DateTime.MaxValue,
                PremiumNotification = notification,

                MonthlyAllowance = 0,
                UsedThisMonth = 0,
                RemainingThisMonth = (int)balanceDetails.TotalCoins
            };

            var subscriptions = await _lwCoinRepository.GetUserSubscriptionsAsync(userId);
            var activeSubscription = subscriptions
                .Where(s => s.Type.StartsWith("coin_subscription_") &&
                            s.ExpiresAt.HasValue &&
                            s.ExpiresAt > DateTime.UtcNow)
                .FirstOrDefault();

            if (activeSubscription != null)
            {
                balanceDto.HasActiveSubscription = true;
                balanceDto.SubscriptionExpiresAt = activeSubscription.ExpiresAt;
                balanceDto.SubscriptionCoinsTotal = ExtractCoinsFromType(activeSubscription.Type);
                balanceDto.SubscriptionCoinsRemaining = (int)balanceDetails.SubscriptionCoins;
            }

            return balanceDto;
        }

        public async Task<bool> SpendLwCoinsAsync(string userId, int legacyAmount, string type, string description, string featureUsed = "")
        {
            var user = await _userRepository.GetByIdAsync(userId);
            if (user == null) return false;

            decimal actualCost = GetActionCost(featureUsed);

            _logger.LogInformation($"🪙 Spending: user={userId}, feature={featureUsed}, cost={actualCost}");

            if (actualCost == 0)
            {
                await CreateTransactionAsync(userId, 0, 0, "free_action",
                    $"Free action: {description}", featureUsed, "free");
                _logger.LogInformation($"✅ Free action for user {userId}: {featureUsed}");
                return true;
            }

            var isPremium = await IsUserPremiumAsync(userId);
            if (isPremium)
            {
                await CreateTransactionAsync(userId, 0, actualCost, "premium_usage",
                    description, featureUsed, "premium");
                _logger.LogInformation($"✅ Premium user {userId} used {featureUsed} for free");
                return true;
            }

            var userFractionalBalance = (decimal)user.FractionalLwCoins;
            if (userFractionalBalance < actualCost)
            {
                _logger.LogWarning($"❌ Insufficient coins: user={userId}, balance={userFractionalBalance}, cost={actualCost}");
                return false;
            }

            await DeductCoinsWithPriority(userId, actualCost);

            _logger.LogInformation($"✅ User {userId} spent {actualCost} coins for {featureUsed}");
            return true;
        }

        private decimal GetActionCost(string featureUsed)
        {
            return featureUsed.ToLowerInvariant() switch
            {
                "photo" or "ai_food_scan" or "food_scan" => PHOTO_FOOD_SCAN_COST,
                "voice" => VOICE_FOOD_SCAN_COST,
                "voice_food" or "ai_voice_food" => VOICE_FOOD_SCAN_COST,
                "voice_workout" or "ai_voice_workout" => VOICE_WORKOUT_COST,

                "text_food" or "ai_text_food" => TEXT_FOOD_SCAN_COST,
                "food_correction" or "ai_food_correction" => FOOD_CORRECTION_COST,
                "text_workout" or "ai_text_workout" => TEXT_WORKOUT_COST,
                "body_analysis" or "ai_body_scan" => BODY_ANALYSIS_COST,

                _ => 0.0m
            };
        }

        private async Task DeductCoinsWithPriority(string userId, decimal amount)
        {
            var user = await _userRepository.GetByIdAsync(userId);
            if (user == null) return;

            var balanceDetails = await GetDetailedBalanceAsync(userId);
            decimal remainingToDeduct = amount;

            if (balanceDetails.SubscriptionCoins > 0 && remainingToDeduct > 0)
            {
                var toDeduct = Math.Min(balanceDetails.SubscriptionCoins, remainingToDeduct);
                remainingToDeduct -= toDeduct;

                await CreateTransactionAsync(userId, -(int)Math.Ceiling(toDeduct), toDeduct,
                    "spent", "Used subscription coins", "", "subscription");
            }

            if (remainingToDeduct > 0)
            {
                await CreateTransactionAsync(userId, -(int)Math.Ceiling(remainingToDeduct), remainingToDeduct,
                    "spent", "Used permanent coins", "", "permanent");
            }

            user.FractionalLwCoins -= (double)amount;
            user.LwCoins = (int)Math.Floor(user.FractionalLwCoins);
            await _userRepository.UpdateAsync(user);
        }

        private async Task<UserCoinBalance> GetDetailedBalanceAsync(string userId)
        {
            var transactions = await _lwCoinRepository.GetUserTransactionsAsync(userId);

            var balance = new UserCoinBalance
            {
                UserId = userId,
                SubscriptionCoins = 0,
                ReferralCoins = 0,
                BonusCoins = 0,
                PermanentCoins = 0,
                RegistrationCoins = 0
            };

            foreach (var transaction in transactions.Where(t => !t.IsExpired))
            {
                var amount = transaction.FractionalAmount > 0 ?
                    (decimal)transaction.FractionalAmount : transaction.Amount;

                switch (transaction.CoinSource)
                {
                    case "subscription":
                        balance.SubscriptionCoins += amount;
                        break;
                    case "referral":
                        balance.ReferralCoins += amount;
                        break;
                    case "bonus":
                        balance.BonusCoins += amount;
                        break;
                    case "registration":
                        balance.RegistrationCoins += amount;
                        break;
                    default:
                        balance.PermanentCoins += amount;
                        break;
                }
            }

            return balance;
        }

        public async Task<bool> AddLwCoinsAsync(string userId, int amount, string type, string description)
        {
            var user = await _userRepository.GetByIdAsync(userId);
            if (user == null) return false;

            user.FractionalLwCoins += amount;
            user.LwCoins = (int)Math.Floor(user.FractionalLwCoins);
            await _userRepository.UpdateAsync(user);

            var coinSource = type switch
            {
                "referral" => "referral",
                "registration_bonus" => "registration",
                "purchase" => "permanent",
                "subscription" => "subscription",
                _ => "bonus"
            };

            await CreateTransactionAsync(userId, amount, amount, type, description, "", coinSource);

            _logger.LogInformation($"💰 User {userId} earned {amount} coins ({coinSource}): {description}");
            return true;
        }

        public async Task<IEnumerable<LwCoinTransactionDto>> GetUserLwCoinTransactionsAsync(string userId)
        {
            var transactions = await _lwCoinRepository.GetUserTransactionsAsync(userId);
            var dtos = _mapper.Map<IEnumerable<LwCoinTransactionDto>>(transactions);

            foreach (var dto in dtos)
            {
                var transaction = transactions.FirstOrDefault(t => t.Id == dto.Id);
                if (transaction != null)
                {
                    dto.CoinSource = transaction.CoinSource;
                    dto.IsExpired = transaction.IsExpired;
                    dto.ExpiryDate = transaction.ExpiryDate;
                }
            }

            return dtos;
        }

        public async Task<bool> PurchaseSubscriptionCoinsAsync(string userId, int coinsAmount, int durationDays, decimal price)
        {
            var user = await _userRepository.GetByIdAsync(userId);
            if (user == null) return false;

            var expiryDate = DateTime.UtcNow.AddDays(durationDays);

            user.FractionalLwCoins += coinsAmount;
            user.LwCoins = (int)Math.Floor(user.FractionalLwCoins);
            await _userRepository.UpdateAsync(user);

            var subscription = new Subscription
            {
                UserId = userId,
                Type = $"coin_subscription_{coinsAmount}",
                Price = price,
                ExpiresAt = expiryDate,
                PaymentTransactionId = Guid.NewGuid().ToString()
            };

            await _lwCoinRepository.CreateSubscriptionAsync(subscription);

            var transaction = new LwCoinTransaction
            {
                UserId = userId,
                Amount = coinsAmount,
                FractionalAmount = coinsAmount,
                Type = "subscription_purchase",
                Description = $"Subscription coins: {coinsAmount} for {durationDays} days",
                CoinSource = "subscription",
                ExpiryDate = expiryDate,
                SubscriptionId = subscription.Id,
                Price = price
            };

            await _lwCoinRepository.CreateTransactionAsync(transaction);

            _logger.LogInformation($"📅 Subscription coins purchased: {coinsAmount} coins for {durationDays} days, expires {expiryDate}");
            return true;
        }

        public async Task<bool> PurchasePremiumAsync(string userId, PurchasePremiumRequest request)
        {
            var user = await _userRepository.GetByIdAsync(userId);
            if (user == null) return false;

            var subscription = new Subscription
            {
                UserId = userId,
                Type = "premium",
                Price = request.Price,
                ExpiresAt = DateTime.UtcNow.AddMonths(1),
                PaymentTransactionId = request.PaymentTransactionId
            };

            await _lwCoinRepository.CreateSubscriptionAsync(subscription);

            await CreateTransactionAsync(userId, 0, 0, "purchase",
                "Premium subscription purchased", "premium", "premium", request.Price, request.Period);

            _logger.LogInformation($"👑 Premium subscription purchased for user {userId}");
            return true;
        }

        public async Task<bool> PurchaseCoinPackAsync(string userId, PurchaseCoinPackRequest request)
        {
            var user = await _userRepository.GetByIdAsync(userId);
            if (user == null) return false;

            int coinsToAdd = request.PackType switch
            {
                "pack_50" => 50,
                "pack_100" => 100,
                "pack_200" => 200,
                "pack_500" => 500,
                _ => 0
            };

            if (coinsToAdd == 0) return false;

            user.FractionalLwCoins += coinsToAdd;
            user.LwCoins = (int)Math.Floor(user.FractionalLwCoins);
            await _userRepository.UpdateAsync(user);

            var subscription = new Subscription
            {
                UserId = userId,
                Type = request.PackType,
                Price = request.Price,
                PaymentTransactionId = request.PaymentTransactionId
            };

            await _lwCoinRepository.CreateSubscriptionAsync(subscription);

            await CreateTransactionAsync(userId, coinsToAdd, coinsToAdd, "purchase",
                $"Purchased {request.PackType} ({coinsToAdd} coins)", "", "permanent", request.Price, "one-time");

            _logger.LogInformation($"💰 Coin pack {request.PackType} purchased: +{coinsToAdd} permanent coins");
            return true;
        }

        public async Task<bool> ProcessMonthlyRefillAsync(string userId)
        {
            try
            {

                _logger.LogInformation($"ProcessMonthlyRefillAsync called for user {userId} - no action taken (monthly refills disabled)");

                await Task.CompletedTask;
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError($"❌ Error in ProcessMonthlyRefillAsync: {ex.Message}");
                return false;
            }
        }

        public async Task<LwCoinLimitsDto> GetUserLimitsAsync(string userId)
        {
            var balanceDetails = await GetDetailedBalanceAsync(userId);
            var isPremium = await IsUserPremiumAsync(userId);
            var featureUsage = await GetFeatureUsageThisMonthAsync(userId);

            return new LwCoinLimitsDto
            {
                MonthlyAllowance = 0,
                UsedThisMonth = (int)featureUsage.Values.Sum(),
                RemainingThisMonth = isPremium ? int.MaxValue : (int)balanceDetails.TotalCoins,
                IsPremium = isPremium,
                FeatureUsage = featureUsage,

                DetailedBalance = new
                {
                    Subscription = balanceDetails.SubscriptionCoins,
                    Permanent = balanceDetails.PermanentTotal,
                    Total = balanceDetails.TotalCoins
                }
            };
        }

        public async Task<SubscriptionStatusDto> GetSubscriptionStatusAsync(string userId)
        {
            var subscriptions = await _lwCoinRepository.GetUserSubscriptionsAsync(userId);
            var activeSubscription = subscriptions
                .Where(s => s.Type.StartsWith("coin_subscription_") &&
                            s.ExpiresAt.HasValue &&
                            s.ExpiresAt > DateTime.UtcNow)
                .FirstOrDefault();

            if (activeSubscription == null)
            {
                return new SubscriptionStatusDto
                {
                    HasActiveSubscription = false
                };
            }

            var coinsAmount = ExtractCoinsFromType(activeSubscription.Type);
            var balanceDetails = await GetDetailedBalanceAsync(userId);

            return new SubscriptionStatusDto
            {
                HasActiveSubscription = true,
                SubscriptionId = activeSubscription.Id,
                ExpiresAt = activeSubscription.ExpiresAt,
                PurchasedCoins = coinsAmount,
                RemainingCoins = (int)balanceDetails.SubscriptionCoins,
                UsedCoins = coinsAmount - (int)balanceDetails.SubscriptionCoins,
                DaysRemaining = activeSubscription.ExpiresAt.HasValue ?
                    (int)(activeSubscription.ExpiresAt.Value - DateTime.UtcNow).TotalDays : 0
            };
        }

        private async Task<bool> IsUserPremiumAsync(string userId)
        {
            var subscriptions = await _lwCoinRepository.GetUserSubscriptionsAsync(userId);
            return subscriptions.Any(s => s.Type == "premium" && s.IsActive &&
                                    (!s.ExpiresAt.HasValue || s.ExpiresAt > DateTime.UtcNow));
        }

        private async Task<DateTime?> GetPremiumExpiryAsync(string userId)
        {
            var subscriptions = await _lwCoinRepository.GetUserSubscriptionsAsync(userId);
            var premiumSub = subscriptions.FirstOrDefault(s => s.Type == "premium" && s.IsActive);
            return premiumSub?.ExpiresAt;
        }

        private async Task<Dictionary<string, int>> GetFeatureUsageThisMonthAsync(string userId)
        {
            var startOfMonth = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1);
            var transactions = await _lwCoinRepository.GetUserTransactionsAsync(userId);

            return transactions
                .Where(t => t.Type == "spent" && t.CreatedAt >= startOfMonth && !string.IsNullOrEmpty(t.FeatureUsed))
                .GroupBy(t => t.FeatureUsed)
                .ToDictionary(g => g.Key, g => g.Count());
        }

        private DateTime GetNextRefillDate(DateTime lastRefill)
        {
            return DateTime.MaxValue;
        }

        private async Task<PremiumNotificationDto?> GeneratePremiumNotificationAsync(string userId, bool isPremium, DateTime? premiumExpiresAt)
        {
            if (!isPremium || !premiumExpiresAt.HasValue)
                return null;

            var daysRemaining = (int)(premiumExpiresAt.Value - DateTime.UtcNow).TotalDays;

            if (daysRemaining <= 3)
            {
                return new PremiumNotificationDto
                {
                    Type = "expiring_soon",
                    Message = $"Premium expires in {daysRemaining} days",
                    ExpiresAt = premiumExpiresAt,
                    DaysRemaining = daysRemaining,
                    IsUrgent = daysRemaining <= 1
                };
            }

            return null;
        }

        private async Task CreateTransactionAsync(string userId, int amount, decimal fractionalAmount,
            string type, string description, string featureUsed = "", string coinSource = "permanent",
            decimal? price = null, string period = "")
        {
            var transaction = new LwCoinTransaction
            {
                UserId = userId,
                Amount = amount,
                FractionalAmount = (double)fractionalAmount,
                Type = type,
                Description = description,
                FeatureUsed = featureUsed,
                CoinSource = coinSource,
                Price = price,
                Period = period,
                UsageDate = DateTime.UtcNow.Date.ToString("yyyy-MM-dd")
            };

            await _lwCoinRepository.CreateTransactionAsync(transaction);
        }

        private int ExtractCoinsFromType(string subscriptionType)
        {
            var parts = subscriptionType.Split('_');
            if (parts.Length == 3 && int.TryParse(parts[2], out var coins))
            {
                return coins;
            }
            return 0;
        }

    }
}
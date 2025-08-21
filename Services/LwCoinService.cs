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

        private const decimal PHOTO_COST = 2.5m;      // Фото-анализ: 2.5 монеты
        private const decimal VOICE_COST = 1.5m;      // Голосовой ввод: 1.5 монеты  
        private const decimal TEXT_COST = 1.0m;       // Текстовый ввод: 1 монета

        private const decimal DAILY_LIMIT_BASE = 10.0m;  

        private const int MONTHLY_ALLOWANCE = 300;
        private const int TRIAL_BONUS = 150;
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

        public async Task<LwCoinBalanceDto> GetUserLwCoinBalanceAsync(string userId)
        {
            var user = await _userRepository.GetByIdAsync(userId);
            if (user == null) throw new ArgumentException("User not found");

            var isPremium = await IsUserPremiumAsync(userId);
            var todayUsage = await GetTodayUsageAsync(userId);
            var premiumExpiresAt = await GetPremiumExpiryAsync(userId);

            var notification = await GeneratePremiumNotificationAsync(userId, isPremium, premiumExpiresAt);

            var dailyRemaining = isPremium ? decimal.MaxValue : decimal.MaxValue; 

            var currentBalance = await GetUserFractionalBalanceAsync(userId);
            var usedThisMonth = await GetUsedCoinsThisMonthAsync(userId);

            return new LwCoinBalanceDto
            {
                Balance = (int)Math.Floor(currentBalance),
                MonthlyAllowance = MONTHLY_ALLOWANCE,
                UsedThisMonth = (int)usedThisMonth,
                RemainingThisMonth = isPremium ? int.MaxValue : Math.Max(0, MONTHLY_ALLOWANCE - (int)usedThisMonth),
                IsPremium = isPremium,
                PremiumExpiresAt = premiumExpiresAt,
                NextRefillDate = GetNextRefillDate(user.LastMonthlyRefill),
                PremiumNotification = notification,

                DailyUsage = todayUsage,
                DailyLimit = isPremium ? decimal.MaxValue : decimal.MaxValue, 
                DailyRemaining = decimal.MaxValue 
            };
        }

        /// <summary>
        /// Пользователь может тратить монеты пока они есть на балансе
        /// </summary>
        public async Task<bool> SpendLwCoinsAsync(string userId, int legacyAmount, string type, string description, string featureUsed = "")
        {
            var user = await _userRepository.GetByIdAsync(userId);
            if (user == null) return false;

            decimal actualCost = GetActionCost(featureUsed);

            _logger.LogInformation($"🪙 Spending: user={userId}, feature={featureUsed}, cost={actualCost}");

            // Check if user is premium (unlimited usage)
            var isPremium = await IsUserPremiumAsync(userId);
            if (isPremium)
            {
                // Premium users don't spend actual coins, but we log the usage
                await CreateTransactionAsync(userId, 0, actualCost, "spent", description, featureUsed, null, "premium");
                _logger.LogInformation($"✅ Premium user {userId} used {featureUsed} for free");
                return true;
            }

            // Check if user has enough coins (используем дробные монеты)
            var userFractionalBalance = await GetUserFractionalBalanceAsync(userId);
            if (userFractionalBalance < actualCost)
            {
                _logger.LogWarning($"❌ Insufficient coins: user={userId}, balance={userFractionalBalance}, cost={actualCost}");
                return false;
            }

            await DeductFractionalCoinsAsync(userId, actualCost);

            // Create transaction record with fractional amount
            await CreateTransactionAsync(userId, -(int)Math.Ceiling(actualCost), actualCost, "spent", description, featureUsed);

            _logger.LogInformation($"✅ User {userId} spent {actualCost} LW Coins for {featureUsed}. Remaining balance: {userFractionalBalance - actualCost}");
            return true;
        }

        /// <summary>
        /// Получение стоимости действия
        /// </summary>
        private decimal GetActionCost(string featureUsed)
        {
            return featureUsed.ToLowerInvariant() switch
            {
                "photo" or "ai_food_scan" or "food_scan" => PHOTO_COST,           
                "voice" or "ai_voice_workout" or "ai_voice_food" => VOICE_COST,   
                "text" or "ai_text" => TEXT_COST,                             
 
                "ai_body_scan" or "body_analysis" => 0.0m,                        
                "exercise" or "activity" => 0.0m,                                

                _ => 1.0m  
            };
        }

        /// <summary>
        /// Получение дробного баланса пользователя
        /// </summary>
        private async Task<decimal> GetUserFractionalBalanceAsync(string userId)
        {
            var user = await _userRepository.GetByIdAsync(userId);
            if (user == null) return 0;

            if (user.FractionalLwCoins <= 0 && user.LwCoins > 0)
            {
                user.FractionalLwCoins = user.LwCoins;
                await _userRepository.UpdateAsync(user);
                _logger.LogInformation($"Initialized FractionalLwCoins for user {userId}: {user.LwCoins}");
            }

            return user.FractionalLwCoins > 0 ? (decimal)user.FractionalLwCoins : user.LwCoins;
        }

        /// <summary>
        /// Списание дробных монет
        /// </summary>
        private async Task DeductFractionalCoinsAsync(string userId, decimal amount)
        {
            var user = await _userRepository.GetByIdAsync(userId);
            if (user == null) return;

            var currentFractional = await GetUserFractionalBalanceAsync(userId);
            var newFractional = currentFractional - amount;

            user.FractionalLwCoins = (double)newFractional;
            user.LwCoins = (int)Math.Floor(newFractional); 

            await _userRepository.UpdateAsync(user);
            _logger.LogInformation($"Deducted {amount} fractional coins from user {userId}. New balance: {newFractional}");
        }

        /// <summary>
        /// ✅ ОБНОВЛЕНО: Получение использования монет за сегодня (только для статистики)
        /// </summary>
        private async Task<decimal> GetTodayUsageAsync(string userId)
        {
            var today = DateTime.UtcNow.Date;
            var transactions = await _lwCoinRepository.GetUserTransactionsAsync(userId);

            return transactions
                .Where(t => t.Type == "spent" && t.CreatedAt.Date == today)
                .Sum(t => t.FractionalAmount > 0 ? (decimal)t.FractionalAmount : Math.Abs(t.Amount));
        }

        public async Task<bool> AddLwCoinsAsync(string userId, int amount, string type, string description)
        {
            var user = await _userRepository.GetByIdAsync(userId);
            if (user == null) return false;

            var currentFractional = await GetUserFractionalBalanceAsync(userId);
            var newFractional = currentFractional + amount;

            user.FractionalLwCoins = (double)newFractional;
            user.LwCoins = (int)Math.Floor(newFractional);
            await _userRepository.UpdateAsync(user);

            await CreateTransactionAsync(userId, amount, amount, type, description);

            _logger.LogInformation($"User {userId} earned {amount} LW Coins: {description}. New balance: {newFractional}");
            return true;
        }

        public async Task<IEnumerable<LwCoinTransactionDto>> GetUserLwCoinTransactionsAsync(string userId)
        {
            var transactions = await _lwCoinRepository.GetUserTransactionsAsync(userId);
            return _mapper.Map<IEnumerable<LwCoinTransactionDto>>(transactions);
        }

        public async Task<bool> ProcessMonthlyRefillAsync(string userId)
        {
            var user = await _userRepository.GetByIdAsync(userId);
            if (user == null) return false;

            // Check if refill is due
            var nextRefillDate = GetNextRefillDate(user.LastMonthlyRefill);
            if (DateTime.UtcNow < nextRefillDate) return false;

            // Add monthly allowance
            user.LwCoins += MONTHLY_ALLOWANCE;
            user.FractionalLwCoins += MONTHLY_ALLOWANCE;
            user.LastMonthlyRefill = DateTime.UtcNow;
            await _userRepository.UpdateAsync(user);

            await CreateTransactionAsync(userId, MONTHLY_ALLOWANCE, MONTHLY_ALLOWANCE, "refill", "Monthly allowance refill", "", null, "monthly");

            _logger.LogInformation($"Monthly refill processed for user {userId}: +{MONTHLY_ALLOWANCE} LW Coins");
            return true;
        }

        public async Task<bool> PurchasePremiumAsync(string userId, PurchasePremiumRequest request)
        {
            var user = await _userRepository.GetByIdAsync(userId);
            if (user == null) return false;

            // Create premium subscription
            var subscription = new Subscription
            {
                UserId = userId,
                Type = "premium",
                Price = request.Price,
                ExpiresAt = DateTime.UtcNow.AddMonths(1),
                PaymentTransactionId = request.PaymentTransactionId
            };

            await _lwCoinRepository.CreateSubscriptionAsync(subscription);

            await CreateTransactionAsync(userId, 0, 0, "purchase", "Premium subscription purchased", "premium", request.Price, request.Period);

            _logger.LogInformation($"Premium subscription purchased for user {userId}");
            return true;
        }

        public async Task<bool> PurchaseCoinPackAsync(string userId, PurchaseCoinPackRequest request)
        {
            var user = await _userRepository.GetByIdAsync(userId);
            if (user == null) return false;

            int coinsToAdd = 0;

            switch (request.PackType)
            {
                case "pack_50":
                    coinsToAdd = 50;
                    break;
                case "pack_100":
                    coinsToAdd = 100;
                    break;
                default:
                    return false;
            }

            // Add coins to user (both integer and fractional)
            user.LwCoins += coinsToAdd;
            user.FractionalLwCoins += coinsToAdd;
            await _userRepository.UpdateAsync(user);

            // Create subscription record
            var subscription = new Subscription
            {
                UserId = userId,
                Type = request.PackType,
                Price = request.Price,
                PaymentTransactionId = request.PaymentTransactionId
            };

            await _lwCoinRepository.CreateSubscriptionAsync(subscription);

            await CreateTransactionAsync(userId, coinsToAdd, coinsToAdd, "purchase", $"Purchased {request.PackType} ({coinsToAdd} LW Coins)", "", request.Price, request.Period);

            _logger.LogInformation($"Coin pack {request.PackType} purchased for user {userId}: +{coinsToAdd} LW Coins");
            return true;
        }

        public async Task<LwCoinLimitsDto> GetUserLimitsAsync(string userId)
        {
            var isPremium = await IsUserPremiumAsync(userId);
            var usedThisMonth = await GetUsedCoinsThisMonthAsync(userId);
            var featureUsage = await GetFeatureUsageThisMonthAsync(userId);
            var todayUsage = await GetTodayUsageAsync(userId);

            return new LwCoinLimitsDto
            {
                MonthlyAllowance = MONTHLY_ALLOWANCE,
                UsedThisMonth = (int)usedThisMonth,
                RemainingThisMonth = isPremium ? int.MaxValue : Math.Max(0, MONTHLY_ALLOWANCE - (int)usedThisMonth),
                IsPremium = isPremium,
                FeatureUsage = featureUsage,

                DailyUsage = todayUsage,
                DailyLimit = decimal.MaxValue, 
                DailyRemaining = decimal.MaxValue 
            };
        }

        private async Task<PremiumNotificationDto?> GeneratePremiumNotificationAsync(string userId, bool isPremium, DateTime? premiumExpiresAt)
        {
            if (!isPremium || !premiumExpiresAt.HasValue)
            {
                var recentExpiredSubscription = await GetRecentExpiredSubscriptionAsync(userId);
                if (recentExpiredSubscription != null)
                {
                    return new PremiumNotificationDto
                    {
                        Type = "downgraded",
                        Message = "Ваша премиум подписка истекла. Вы переведены на стандартный план.",
                        ExpiresAt = null,
                        DaysRemaining = 0,
                        IsUrgent = true
                    };
                }
                return null;
            }

            var daysRemaining = (int)(premiumExpiresAt.Value - DateTime.UtcNow).TotalDays;

            if (daysRemaining <= 0)
            {
                await DowngradePremiumSubscriptionAsync(userId);

                return new PremiumNotificationDto
                {
                    Type = "expired",
                    Message = "Ваша премиум подписка истекла. Вы переведены на стандартный план.",
                    ExpiresAt = premiumExpiresAt,
                    DaysRemaining = 0,
                    IsUrgent = true
                };
            }

            if (daysRemaining <= 3)
            {
                return new PremiumNotificationDto
                {
                    Type = "expiring_soon",
                    Message = $"Ваша премиум подписка истекает через {daysRemaining} дн. Продлите сейчас!",
                    ExpiresAt = premiumExpiresAt,
                    DaysRemaining = daysRemaining,
                    IsUrgent = daysRemaining <= 1
                };
            }

            return null;
        }

        private async Task DowngradePremiumSubscriptionAsync(string userId)
        {
            var subscriptions = await _lwCoinRepository.GetUserSubscriptionsAsync(userId);
            var expiredPremium = subscriptions.FirstOrDefault(s => s.Type == "premium" &&
                s.IsActive && s.ExpiresAt.HasValue && s.ExpiresAt <= DateTime.UtcNow);

            if (expiredPremium != null)
            {
                expiredPremium.IsActive = false;

                await CreateTransactionAsync(userId, 0, 0, "downgrade", "Premium subscription expired - downgraded to standard", "premium", 0, "expired");

                _logger.LogInformation($"User {userId} downgraded from premium to standard due to expiration");
            }
        }

        private async Task<Subscription?> GetRecentExpiredSubscriptionAsync(string userId)
        {
            var subscriptions = await _lwCoinRepository.GetUserSubscriptionsAsync(userId);
            var threeDaysAgo = DateTime.UtcNow.AddDays(-3);

            return subscriptions.FirstOrDefault(s => s.Type == "premium" &&
                !s.IsActive && s.ExpiresAt.HasValue &&
                s.ExpiresAt >= threeDaysAgo && s.ExpiresAt <= DateTime.UtcNow);
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

        private async Task<decimal> GetUsedCoinsThisMonthAsync(string userId)
        {
            var startOfMonth = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1);
            var transactions = await _lwCoinRepository.GetUserTransactionsAsync(userId);

            return transactions
                .Where(t => t.Type == "spent" && t.CreatedAt >= startOfMonth)
                .Sum(t => t.FractionalAmount > 0 ? (decimal)t.FractionalAmount : Math.Abs(t.Amount));
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
            return new DateTime(lastRefill.Year, lastRefill.Month, 1).AddMonths(1);
        }

        /// <summary>
        /// Создание транзакции
        /// </summary>
        private async Task CreateTransactionAsync(string userId, int amount, decimal fractionalAmount, string type, string description, string featureUsed = "", decimal? price = null, string period = "")
        {
            var transaction = new LwCoinTransaction
            {
                UserId = userId,
                Amount = amount,
                FractionalAmount = (double)fractionalAmount,
                Type = type,
                Description = description,
                FeatureUsed = featureUsed,
                Price = price,
                Period = period,
                UsageDate = DateTime.UtcNow.Date.ToString("yyyy-MM-dd")
            };

            await _lwCoinRepository.CreateTransactionAsync(transaction);
        }
    }
}
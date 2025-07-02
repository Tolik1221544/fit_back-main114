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
            var usedThisMonth = await GetUsedCoinsThisMonthAsync(userId);
            var premiumExpiresAt = await GetPremiumExpiryAsync(userId);

            var notification = await GeneratePremiumNotificationAsync(userId, isPremium, premiumExpiresAt);

            return new LwCoinBalanceDto
            {
                Balance = user.LwCoins,
                MonthlyAllowance = MONTHLY_ALLOWANCE,
                UsedThisMonth = usedThisMonth,
                RemainingThisMonth = isPremium ? int.MaxValue : Math.Max(0, MONTHLY_ALLOWANCE - usedThisMonth),
                IsPremium = isPremium,
                PremiumExpiresAt = premiumExpiresAt,
                NextRefillDate = GetNextRefillDate(user.LastMonthlyRefill),
                PremiumNotification = notification
            };
        }

        public async Task<bool> SpendLwCoinsAsync(string userId, int amount, string type, string description, string featureUsed = "")
        {
            var user = await _userRepository.GetByIdAsync(userId);
            if (user == null) return false;

            // Check if user is premium (unlimited usage)
            var isPremium = await IsUserPremiumAsync(userId);
            if (isPremium)
            {
                // Premium users don't spend actual coins, but we log the usage
                await CreateTransactionAsync(userId, 0, "spent", description, featureUsed, null, "premium");
                return true;
            }

            // Check if user has enough coins
            if (user.LwCoins < amount) return false;

            // Deduct coins
            user.LwCoins -= amount;
            await _userRepository.UpdateAsync(user);

            // Create transaction record
            await CreateTransactionAsync(userId, -amount, "spent", description, featureUsed);

            _logger.LogInformation($"User {userId} spent {amount} LW Coins for {featureUsed}");
            return true;
        }

        public async Task<bool> AddLwCoinsAsync(string userId, int amount, string type, string description)
        {
            var user = await _userRepository.GetByIdAsync(userId);
            if (user == null) return false;

            user.LwCoins += amount;
            await _userRepository.UpdateAsync(user);

            await CreateTransactionAsync(userId, amount, type, description);

            _logger.LogInformation($"User {userId} earned {amount} LW Coins: {description}");
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
            user.LastMonthlyRefill = DateTime.UtcNow;
            await _userRepository.UpdateAsync(user);

            await CreateTransactionAsync(userId, MONTHLY_ALLOWANCE, "refill", "Monthly allowance refill", "", null, "monthly");

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

            await CreateTransactionAsync(userId, 0, "purchase", "Premium subscription purchased", "premium", request.Price, request.Period);

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

            // Add coins to user
            user.LwCoins += coinsToAdd;
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

            await CreateTransactionAsync(userId, coinsToAdd, "purchase", $"Purchased {request.PackType} ({coinsToAdd} LW Coins)", "", request.Price, request.Period);

            _logger.LogInformation($"Coin pack {request.PackType} purchased for user {userId}: +{coinsToAdd} LW Coins");
            return true;
        }

        public async Task<LwCoinLimitsDto> GetUserLimitsAsync(string userId)
        {
            var isPremium = await IsUserPremiumAsync(userId);
            var usedThisMonth = await GetUsedCoinsThisMonthAsync(userId);
            var featureUsage = await GetFeatureUsageThisMonthAsync(userId);

            return new LwCoinLimitsDto
            {
                MonthlyAllowance = MONTHLY_ALLOWANCE,
                UsedThisMonth = usedThisMonth,
                RemainingThisMonth = isPremium ? int.MaxValue : Math.Max(0, MONTHLY_ALLOWANCE - usedThisMonth),
                IsPremium = isPremium,
                FeatureUsage = featureUsage
            };
        }


        private async Task<PremiumNotificationDto?> GeneratePremiumNotificationAsync(string userId, bool isPremium, DateTime? premiumExpiresAt)
        {
            if (!isPremium || !premiumExpiresAt.HasValue)
            {
                // ���������, ���� �� ������� �������� ��������
                var recentExpiredSubscription = await GetRecentExpiredSubscriptionAsync(userId);
                if (recentExpiredSubscription != null)
                {
                    return new PremiumNotificationDto
                    {
                        Type = "downgraded",
                        Message = "���� ������� �������� �������. �� ���������� �� ����������� ����.",
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
                // �������� ������� - ������������� ��������� �� ����������� ����
                await DowngradePremiumSubscriptionAsync(userId);

                return new PremiumNotificationDto
                {
                    Type = "expired",
                    Message = "���� ������� �������� �������. �� ���������� �� ����������� ����.",
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
                    Message = $"���� ������� �������� �������� ����� {daysRemaining} ��. �������� ������!",
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
                // ����� ������ ���� ����� ��� ���������� �������� � �����������

                await CreateTransactionAsync(userId, 0, "downgrade", "Premium subscription expired - downgraded to standard", "premium", 0, "expired");

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

        // Helper methods
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

        private async Task<int> GetUsedCoinsThisMonthAsync(string userId)
        {
            var startOfMonth = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1);
            var transactions = await _lwCoinRepository.GetUserTransactionsAsync(userId);

            return transactions
                .Where(t => t.Type == "spent" && t.CreatedAt >= startOfMonth)
                .Sum(t => Math.Abs(t.Amount));
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

        private async Task CreateTransactionAsync(string userId, int amount, string type, string description, string featureUsed = "", decimal? price = null, string period = "")
        {
            var transaction = new LwCoinTransaction
            {
                UserId = userId,
                Amount = amount,
                Type = type,
                Description = description,
                FeatureUsed = featureUsed,
                Price = price,
                Period = period
            };

            await _lwCoinRepository.CreateTransactionAsync(transaction);
        }
    }
}
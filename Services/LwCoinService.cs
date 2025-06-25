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

            return new LwCoinBalanceDto
            {
                Balance = user.LwCoins,
                MonthlyAllowance = MONTHLY_ALLOWANCE,
                UsedThisMonth = usedThisMonth,
                RemainingThisMonth = isPremium ? int.MaxValue : Math.Max(0, MONTHLY_ALLOWANCE - usedThisMonth),
                IsPremium = isPremium,
                PremiumExpiresAt = await GetPremiumExpiryAsync(userId),
                NextRefillDate = GetNextRefillDate(user.LastMonthlyRefill)
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
                await CreateTransactionAsync(userId, 0, "spent", description, featureUsed);
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

            await CreateTransactionAsync(userId, MONTHLY_ALLOWANCE, "refill", "Monthly allowance refill");

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
                Price = PREMIUM_PRICE,
                ExpiresAt = DateTime.UtcNow.AddMonths(1),
                PaymentTransactionId = request.PaymentTransactionId
            };

            await _lwCoinRepository.CreateSubscriptionAsync(subscription);

            await CreateTransactionAsync(userId, 0, "purchase", "Premium subscription purchased");

            _logger.LogInformation($"Premium subscription purchased for user {userId}");
            return true;
        }

        public async Task<bool> PurchaseCoinPackAsync(string userId, PurchaseCoinPackRequest request)
        {
            var user = await _userRepository.GetByIdAsync(userId);
            if (user == null) return false;

            int coinsToAdd = 0;
            decimal price = 0;

            switch (request.PackType)
            {
                case "pack_50":
                    coinsToAdd = 50;
                    price = 0.50m;
                    break;
                case "pack_100":
                    coinsToAdd = 100;
                    price = 1.00m;
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
                Price = price,
                PaymentTransactionId = request.PaymentTransactionId
            };

            await _lwCoinRepository.CreateSubscriptionAsync(subscription);

            await CreateTransactionAsync(userId, coinsToAdd, "purchase", $"Purchased {request.PackType} ({coinsToAdd} LW Coins)");

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

        private async Task CreateTransactionAsync(string userId, int amount, string type, string description, string featureUsed = "")
        {
            var transaction = new LwCoinTransaction
            {
                UserId = userId,
                Amount = amount,
                Type = type,
                Description = description,
                FeatureUsed = featureUsed
            };

            await _lwCoinRepository.CreateTransactionAsync(transaction);
        }
    }
}
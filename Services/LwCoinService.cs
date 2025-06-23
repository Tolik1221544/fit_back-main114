using FitnessTracker.API.DTOs;
using FitnessTracker.API.Models;
using FitnessTracker.API.Repositories;
using AutoMapper;

namespace FitnessTracker.API.Services
{
    public class LwCoinService : ILwCoinService
    {
        private readonly IUserRepository _userRepository;
        private readonly ILwCoinRepository _lwCoinRepository;
        private readonly IMapper _mapper;

        public LwCoinService(IUserRepository userRepository, ILwCoinRepository lwCoinRepository, IMapper mapper)
        {
            _userRepository = userRepository;
            _lwCoinRepository = lwCoinRepository;
            _mapper = mapper;
        }

        public async Task<LwCoinBalanceDto> GetUserLwCoinBalanceAsync(string userId)
        {
            var user = await _userRepository.GetByIdAsync(userId);
            if (user == null) throw new ArgumentException("User not found");

            // Проверяем и обновляем месячный лимит
            await ProcessMonthlyRefillAsync(userId);

            var hasPremium = user.HasPremiumSubscription &&
                            user.PremiumExpiresAt.HasValue &&
                            user.PremiumExpiresAt.Value > DateTime.UtcNow;

            return new LwCoinBalanceDto
            {
                Balance = user.LwCoins,
                MonthlyUsed = user.MonthlyLwCoinsUsed,
                MonthlyLimit = hasPremium ? -1 : 300, // -1 = безлимит
                HasPremium = hasPremium,
                PremiumExpiresAt = user.PremiumExpiresAt,
                DaysUntilRefill = GetDaysUntilNextRefill(user.CurrentMonthStart)
            };
        }

        public async Task<bool> SpendLwCoinsAsync(string userId, int amount, string type, string description, string featureUsed = "")
        {
            var user = await _userRepository.GetByIdAsync(userId);
            if (user == null) return false;

            // Проверяем премиум
            var hasPremium = user.HasPremiumSubscription &&
                            user.PremiumExpiresAt.HasValue &&
                            user.PremiumExpiresAt.Value > DateTime.UtcNow;

            // Если премиум - трата бесплатная
            if (hasPremium)
            {
                await _lwCoinRepository.CreateTransactionAsync(new LwCoinTransaction
                {
                    UserId = userId,
                    Amount = 0, // Бесплатно для премиум
                    Type = type,
                    Description = $"{description} (Premium)",
                    FeatureUsed = featureUsed
                });
                return true;
            }

            // Проверяем баланс
            if (user.LwCoins < amount) return false;

            // Списываем монеты
            user.LwCoins -= amount;
            user.MonthlyLwCoinsUsed += amount;
            await _userRepository.UpdateAsync(user);

            // Записываем транзакцию
            await _lwCoinRepository.CreateTransactionAsync(new LwCoinTransaction
            {
                UserId = userId,
                Amount = -amount,
                Type = type,
                Description = description,
                FeatureUsed = featureUsed
            });

            return true;
        }

        public async Task<bool> AddLwCoinsAsync(string userId, int amount, string type, string description)
        {
            var user = await _userRepository.GetByIdAsync(userId);
            if (user == null) return false;

            user.LwCoins += amount;
            await _userRepository.UpdateAsync(user);

            await _lwCoinRepository.CreateTransactionAsync(new LwCoinTransaction
            {
                UserId = userId,
                Amount = amount,
                Type = type,
                Description = description
            });

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

            var now = DateTime.UtcNow;
            var monthsDiff = (now.Year - user.CurrentMonthStart.Year) * 12 + now.Month - user.CurrentMonthStart.Month;

            if (monthsDiff >= 1)
            {
                user.LwCoins = 300;
                user.MonthlyLwCoinsUsed = 0;
                user.CurrentMonthStart = new DateTime(now.Year, now.Month, 1);
                user.LastMonthlyRefill = now;
                await _userRepository.UpdateAsync(user);

                // Записываем транзакцию
                await _lwCoinRepository.CreateTransactionAsync(new LwCoinTransaction
                {
                    UserId = userId,
                    Amount = 300,
                    Type = "monthly_refill",
                    Description = "Monthly LW Coins refill"
                });

                return true;
            }

            return false;
        }

        public async Task<bool> PurchasePremiumAsync(string userId, PurchasePremiumRequest request)
        {
            var user = await _userRepository.GetByIdAsync(userId);
            if (user == null) return false;

            user.HasPremiumSubscription = true;
            user.PremiumExpiresAt = DateTime.UtcNow.AddMonths(1);
            await _userRepository.UpdateAsync(user);

            // Записываем подписку
            await _lwCoinRepository.CreateSubscriptionAsync(new Subscription
            {
                UserId = userId,
                Type = "premium",
                Price = 8.99m,
                ExpiresAt = user.PremiumExpiresAt,
                PaymentTransactionId = request.PaymentTransactionId
            });

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
                _ => 0
            };

            if (coinsToAdd == 0) return false;

            user.LwCoins += coinsToAdd;
            await _userRepository.UpdateAsync(user);

            // Записываем транзакцию
            await _lwCoinRepository.CreateTransactionAsync(new LwCoinTransaction
            {
                UserId = userId,
                Amount = coinsToAdd,
                Type = "purchase",
                Description = $"Purchased {coinsToAdd} LW Coins"
            });

            // Записываем подписку
            await _lwCoinRepository.CreateSubscriptionAsync(new Subscription
            {
                UserId = userId,
                Type = request.PackType,
                Price = request.PackType == "pack_50" ? 0.50m : 1.00m,
                PaymentTransactionId = request.PaymentTransactionId
            });

            return true;
        }

        public async Task<LwCoinLimitsDto> GetUserLimitsAsync(string userId)
        {
            var user = await _userRepository.GetByIdAsync(userId);
            if (user == null) throw new ArgumentException("User not found");

            var hasPremium = user.HasPremiumSubscription &&
                            user.PremiumExpiresAt.HasValue &&
                            user.PremiumExpiresAt.Value > DateTime.UtcNow;

            return new LwCoinLimitsDto
            {
                CanUseFeature = hasPremium || user.LwCoins > 0,
                CostPerUse = hasPremium ? 0 : 1,
                RemainingCoins = user.LwCoins,
                HasPremium = hasPremium,
                LimitType = hasPremium ? "unlimited" : "monthly"
            };
        }

        private int GetDaysUntilNextRefill(DateTime currentMonthStart)
        {
            var nextMonth = currentMonthStart.AddMonths(1);
            return Math.Max(0, (nextMonth - DateTime.UtcNow).Days);
        }
    }
}
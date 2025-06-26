using FitnessTracker.API.DTOs;
using FitnessTracker.API.Models;
using FitnessTracker.API.Repositories;
using AutoMapper;

namespace FitnessTracker.API.Services
{
    public class ReferralService : IReferralService
    {
        private readonly IReferralRepository _referralRepository;
        private readonly IUserRepository _userRepository;
        private readonly ILwCoinService _lwCoinService;
        private readonly IMapper _mapper;
        private readonly ILogger<ReferralService> _logger;

        private const int REFERRAL_REWARD = 150;

        public ReferralService(
            IReferralRepository referralRepository,
            IUserRepository userRepository,
            ILwCoinService lwCoinService,
            IMapper mapper,
            ILogger<ReferralService> logger)
        {
            _referralRepository = referralRepository;
            _userRepository = userRepository;
            _lwCoinService = lwCoinService;
            _mapper = mapper;
            _logger = logger;
        }

        public async Task<bool> SetReferralAsync(string userId, SetReferralRequest request)
        {
            var user = await _userRepository.GetByIdAsync(userId);
            if (user == null)
            {
                _logger.LogWarning($"User {userId} not found");
                return false;
            }

            // Check if user already has a referrer
            if (!string.IsNullOrEmpty(user.ReferredByUserId))
            {
                _logger.LogWarning($"User {userId} already has a referrer");
                return false;
            }

            // Find referrer by their referral code
            var referrer = await _userRepository.GetByReferralCodeAsync(request.ReferralCode);
            if (referrer == null)
            {
                _logger.LogWarning($"Invalid referral code: {request.ReferralCode}");
                return false;
            }

            // Can't refer yourself
            if (referrer.Id == userId)
            {
                _logger.LogWarning($"User {userId} tried to use their own referral code");
                return false;
            }

            // Set referrer
            user.ReferredByUserId = referrer.Id;
            await _userRepository.UpdateAsync(user);

            // Create referral record
            var newReferral = new Referral
            {
                ReferrerId = referrer.Id,
                ReferredUserId = userId,
                ReferralCode = request.ReferralCode,
                RewardCoins = REFERRAL_REWARD
            };

            await _referralRepository.CreateAsync(newReferral);

            // Update referrer stats (1-й уровень)
            referrer.TotalReferrals++;
            referrer.TotalReferralRewards += REFERRAL_REWARD;
            await _userRepository.UpdateAsync(referrer);

            // Give LW Coins to 1-го уровня referrer
            await _lwCoinService.AddLwCoinsAsync(referrer.Id, REFERRAL_REWARD, "referral",
                $"Referral bonus for inviting {user.Name ?? user.Email}");

            // 🎯 ПРОВЕРЯЕМ 2-Й УРОВЕНЬ: если у referrer есть свой referrer
            if (!string.IsNullOrEmpty(referrer.ReferredByUserId))
            {
                var secondLevelReferrer = await _userRepository.GetByIdAsync(referrer.ReferredByUserId);
                if (secondLevelReferrer != null)
                {
                    var secondLevelReward = REFERRAL_REWARD / 2; // 50% от основной награды

                    // Обновляем статистику 2-го уровня
                    secondLevelReferrer.TotalReferralRewards += secondLevelReward;
                    await _userRepository.UpdateAsync(secondLevelReferrer);

                    // Начисляем LW Coins 2-го уровня
                    await _lwCoinService.AddLwCoinsAsync(secondLevelReferrer.Id, secondLevelReward, "referral_level2",
                        $"Level 2 referral bonus for {user.Name ?? user.Email} (via {referrer.Name ?? referrer.Email})");

                    _logger.LogInformation($"Level 2 referral reward: {secondLevelReferrer.Id} got {secondLevelReward} LW Coins");
                }
            }

            // Give trial bonus to new user
            await _lwCoinService.AddLwCoinsAsync(userId, REFERRAL_REWARD, "trial_bonus",
                "Welcome bonus for joining via referral");

            _logger.LogInformation($"Referral set successfully: {referrer.Id} -> {userId}");
            return true;
        }

        public async Task<string> GenerateReferralCodeAsync(string userId)
        {
            var user = await _userRepository.GetByIdAsync(userId);
            if (user == null) throw new ArgumentException("User not found");

            // If user already has a referral code, return it
            if (!string.IsNullOrEmpty(user.ReferralCode))
            {
                return user.ReferralCode;
            }

            // Generate unique referral code
            string referralCode;
            do
            {
                referralCode = GenerateUniqueCode();
            } while (await _referralRepository.CodeExistsAsync(referralCode));

            // Save referral code to user
            user.ReferralCode = referralCode;
            await _userRepository.UpdateAsync(user);

            _logger.LogInformation($"Generated referral code {referralCode} for user {userId}");
            return referralCode;
        }

        public async Task<int> GetReferralCountAsync(string userId)
        {
            var user = await _userRepository.GetByIdAsync(userId);
            return user?.TotalReferrals ?? 0;
        }

        public async Task<ReferralStatsDto> GetReferralStatsAsync(string userId)
        {
            var user = await _userRepository.GetByIdAsync(userId);
            if (user == null) throw new ArgumentException("User not found");

            // Получаем рефералов 1-го уровня (прямые приглашения)
            var firstLevelReferrals = await _referralRepository.GetUserReferralsAsync(userId);
            var currentMonth = DateTime.UtcNow.Month;
            var currentYear = DateTime.UtcNow.Year;

            var monthlyReferrals = firstLevelReferrals.Count(r => r.CreatedAt.Month == currentMonth && r.CreatedAt.Year == currentYear);
            var monthlyEarnedCoins = monthlyReferrals * REFERRAL_REWARD;

            // Получаем детали рефералов 1-го уровня
            var firstLevelUsers = new List<ReferredUserDto>();
            var secondLevelUsers = new List<ReferredUserDto>();

            foreach (var referral in firstLevelReferrals.OrderByDescending(r => r.CreatedAt))
            {
                var referredUser = await _userRepository.GetByIdAsync(referral.ReferredUserId);
                if (referredUser != null)
                {
                    var isPremium = await IsUserPremiumAsync(referral.ReferredUserId);

                    // Добавляем пользователя 1-го уровня
                    firstLevelUsers.Add(new ReferredUserDto
                    {
                        Email = MaskEmail(referredUser.Email),
                        Name = MaskName(referredUser.Name),
                        Level = referredUser.Level,
                        JoinedAt = referral.CreatedAt,
                        RewardCoins = referral.RewardCoins,
                        IsPremium = isPremium,
                        Status = GetUserStatus(referredUser),
                        ReferralLevel = 1
                    });

                    // Получаем рефералов 2-го уровня (рефералы моих рефералов)
                    var secondLevelReferrals = await _referralRepository.GetUserReferralsAsync(referral.ReferredUserId);

                    foreach (var secondReferral in secondLevelReferrals)
                    {
                        var secondLevelUser = await _userRepository.GetByIdAsync(secondReferral.ReferredUserId);
                        if (secondLevelUser != null)
                        {
                            var isSecondLevelPremium = await IsUserPremiumAsync(secondReferral.ReferredUserId);

                            secondLevelUsers.Add(new ReferredUserDto
                            {
                                Email = MaskEmail(secondLevelUser.Email),
                                Name = MaskName(secondLevelUser.Name),
                                Level = secondLevelUser.Level,
                                JoinedAt = secondReferral.CreatedAt,
                                RewardCoins = secondReferral.RewardCoins / 2, // 2-й уровень получает 50% от награды
                                IsPremium = isSecondLevelPremium,
                                Status = GetUserStatus(secondLevelUser),
                                ReferralLevel = 2
                            });
                        }
                    }
                }
            }

            // Подсчитываем общее количество рефералов и заработанных монет
            var totalReferrals = firstLevelUsers.Count + secondLevelUsers.Count;
            var totalEarnedCoins = firstLevelUsers.Sum(u => u.RewardCoins) + secondLevelUsers.Sum(u => u.RewardCoins);

            // Вычисляем месячные показатели для 2-го уровня
            var monthlySecondLevel = secondLevelUsers.Count(u => u.JoinedAt.Month == currentMonth && u.JoinedAt.Year == currentYear);
            var monthlyEarnedFromSecondLevel = monthlySecondLevel * (REFERRAL_REWARD / 2);

            // Рассчитываем ранг
            var rank = await CalculateUserRankAsync(userId);

            // Получаем лидерборд
            var leaderboard = await GetLeaderboardAsync(userId);

            return new ReferralStatsDto
            {
                UserId = userId,
                Email = MaskEmail(user.Email),
                Name = MaskName(user.Name),
                ReferralCode = user.ReferralCode ?? await GenerateReferralCodeAsync(userId),
                Level = user.Level,
                TotalReferrals = totalReferrals,
                MonthlyReferrals = monthlyReferrals + monthlySecondLevel,
                TotalEarnedCoins = totalEarnedCoins,
                MonthlyEarnedCoins = monthlyEarnedCoins + monthlyEarnedFromSecondLevel,
                FirstLevelReferrals = firstLevelUsers,
                SecondLevelReferrals = secondLevelUsers,
                Rank = rank,
                Leaderboard = leaderboard
            };
        }

        public async Task<GenerateReferralResponse> GenerateReferralLinkAsync(string userId)
        {
            var referralCode = await GenerateReferralCodeAsync(userId);
            var baseUrl = "https://your-app.com"; // Replace with actual app URL

            return new GenerateReferralResponse
            {
                ReferralCode = referralCode,
                ReferralLink = $"{baseUrl}/join?ref={referralCode}",
                QrCodeUrl = $"{baseUrl}/api/referral/qr/{referralCode}"
            };
        }

        public async Task<ValidateReferralResponse> ValidateReferralCodeAsync(string referralCode)
        {
            if (string.IsNullOrWhiteSpace(referralCode))
            {
                return new ValidateReferralResponse
                {
                    IsValid = false,
                    Message = "Referral code is required"
                };
            }

            var referrer = await _userRepository.GetByReferralCodeAsync(referralCode);

            if (referrer == null)
            {
                return new ValidateReferralResponse
                {
                    IsValid = false,
                    Message = "Invalid referral code"
                };
            }

            return new ValidateReferralResponse
            {
                IsValid = true,
                ReferrerEmail = MaskEmail(referrer.Email),
                Message = $"Valid referral code from {MaskEmail(referrer.Email)}"
            };
        }

        // Helper methods
        private string GenerateUniqueCode()
        {
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
            var random = new Random();
            return new string(Enumerable.Repeat(chars, 8)
                .Select(s => s[random.Next(s.Length)]).ToArray());
        }

        private string MaskEmail(string email)
        {
            if (string.IsNullOrEmpty(email) || !email.Contains("@"))
                return email;

            var parts = email.Split('@');
            var username = parts[0];
            var domain = parts[1];

            if (username.Length <= 2)
                return $"{username}@{domain}";

            var maskedUsername = username[0] + new string('*', username.Length - 2) + username[^1];
            return $"{maskedUsername}@{domain}";
        }

        private string MaskName(string name)
        {
            if (string.IsNullOrEmpty(name))
                return "Аноним";

            if (name.Length <= 2)
                return name;

            return name[0] + new string('*', Math.Max(0, name.Length - 2)) + name[^1];
        }

        private async Task<bool> IsUserPremiumAsync(string userId)
        {
            // This should check if user has active premium subscription
            // Implementation depends on your subscription logic
            return false; // Placeholder
        }

        private string GetUserStatus(User user)
        {
            var daysSinceLastActivity = (DateTime.UtcNow - user.JoinedAt).Days;
            return daysSinceLastActivity <= 7 ? "Active" : "Inactive";
        }

        private async Task<ReferralRankDto> CalculateUserRankAsync(string userId)
        {
            var allUsers = await GetAllUsersAsync();

            // Подсчитываем общее количество рефералов (1-й + 2-й уровень) для каждого пользователя
            var userReferralCounts = new Dictionary<string, int>();

            foreach (var user in allUsers)
            {
                var firstLevel = await _referralRepository.GetUserReferralsAsync(user.Id);
                var secondLevelCount = 0;

                foreach (var firstRef in firstLevel)
                {
                    var secondLevel = await _referralRepository.GetUserReferralsAsync(firstRef.ReferredUserId);
                    secondLevelCount += secondLevel.Count();
                }

                userReferralCounts[user.Id] = firstLevel.Count() + secondLevelCount;
            }

            var sortedUsers = userReferralCounts.OrderByDescending(kvp => kvp.Value).ToList();
            var userPosition = sortedUsers.FindIndex(kvp => kvp.Key == userId) + 1;
            var currentReferrals = userReferralCounts.GetValueOrDefault(userId, 0);

            // Define rank tiers (обновленные пороги)
            string title;
            string badge;
            int nextLevelRequirement;

            if (currentReferrals == 0)
            {
                title = "Новичок";
                badge = "🌱";
                nextLevelRequirement = 1;
            }
            else if (currentReferrals < 3)
            {
                title = "Начинающий";
                badge = "⭐";
                nextLevelRequirement = 3;
            }
            else if (currentReferrals < 10)
            {
                title = "Активный";
                badge = "🌟";
                nextLevelRequirement = 10;
            }
            else if (currentReferrals < 25)
            {
                title = "Чемпион";
                badge = "🏆";
                nextLevelRequirement = 25;
            }
            else if (currentReferrals < 50)
            {
                title = "Легенда";
                badge = "👑";
                nextLevelRequirement = 50;
            }
            else
            {
                title = "Мастер";
                badge = "💎";
                nextLevelRequirement = 100;
            }

            return new ReferralRankDto
            {
                Position = userPosition,
                Title = title,
                Badge = badge,
                NextLevelRequirement = nextLevelRequirement,
                Progress = currentReferrals
            };
        }

        private async Task<List<ReferralLeaderboardDto>> GetLeaderboardAsync(string currentUserId)
        {
            var allUsers = await GetAllUsersAsync();
            var topUsers = allUsers
                .Where(u => u.TotalReferrals > 0)
                .OrderByDescending(u => u.TotalReferrals)
                .Take(10)
                .ToList();

            var leaderboard = new List<ReferralLeaderboardDto>();
            for (int i = 0; i < topUsers.Count; i++)
            {
                var user = topUsers[i];
                var currentMonth = DateTime.UtcNow.Month;
                var currentYear = DateTime.UtcNow.Year;

                var userReferrals = await _referralRepository.GetUserReferralsAsync(user.Id);
                var monthlyReferrals = userReferrals.Count(r => r.CreatedAt.Month == currentMonth && r.CreatedAt.Year == currentYear);

                var rank = await CalculateUserRankAsync(user.Id);

                leaderboard.Add(new ReferralLeaderboardDto
                {
                    Position = i + 1,
                    Email = MaskEmail(user.Email),
                    Level = user.Level,
                    TotalReferrals = user.TotalReferrals,
                    MonthlyReferrals = monthlyReferrals,
                    Badge = rank.Badge,
                    IsCurrentUser = user.Id == currentUserId
                });
            }

            return leaderboard;
        }

        private async Task<List<User>> GetAllUsersAsync()
        {
            var users = await _userRepository.GetAllUsersAsync();
            return users.ToList();
        }
    }
}
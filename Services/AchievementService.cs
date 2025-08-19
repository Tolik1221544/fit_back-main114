using FitnessTracker.API.DTOs;
using FitnessTracker.API.Models;
using FitnessTracker.API.Repositories;
using AutoMapper;

namespace FitnessTracker.API.Services
{
    public class AchievementService : IAchievementService
    {
        private readonly IAchievementRepository _achievementRepository;
        private readonly IUserRepository _userRepository;
        private readonly IActivityRepository _activityRepository;
        private readonly IFoodIntakeRepository _foodIntakeRepository;
        private readonly IReferralRepository _referralRepository;
        private readonly IExperienceService _experienceService;
        private readonly ILocalizationService _localizationService; 
        private readonly IMapper _mapper;
        private readonly ILogger<AchievementService> _logger;

        public AchievementService(
            IAchievementRepository achievementRepository,
            IUserRepository userRepository,
            IActivityRepository activityRepository,
            IFoodIntakeRepository foodIntakeRepository,
            IReferralRepository referralRepository,
            IExperienceService experienceService,
            ILocalizationService localizationService, 
            IMapper mapper,
            ILogger<AchievementService> logger)
        {
            _achievementRepository = achievementRepository;
            _userRepository = userRepository;
            _activityRepository = activityRepository;
            _foodIntakeRepository = foodIntakeRepository;
            _referralRepository = referralRepository;
            _experienceService = experienceService;
            _localizationService = localizationService; 
            _mapper = mapper;
            _logger = logger;
        }

        public async Task<IEnumerable<AchievementDto>> GetUserAchievementsAsync(string userId)
        {
            var achievements = await _achievementRepository.GetAllAchievementsAsync();
            var userAchievements = await _achievementRepository.GetUserAchievementsAsync(userId);
            var userAchievementDict = userAchievements.ToDictionary(ua => ua.AchievementId);

            var userLocale = await _localizationService.GetUserLocaleAsync(userId);

            var achievementDtos = new List<AchievementDto>();

            foreach (var achievement in achievements.Where(a => a.IsActive))
            {
                var currentProgress = await CalculateProgressAsync(userId, achievement.Type);
                var isUnlocked = userAchievementDict.ContainsKey(achievement.Id);

                var achievementKey = achievement.Id.Replace("achievement_", "achievement.");

                var achievementDto = new AchievementDto
                {
                    Id = achievement.Id,
                    Title = _localizationService.Translate(achievementKey, userLocale),
                    Icon = achievement.Icon,
                    ImageUrl = achievement.ImageUrl,
                    Type = achievement.Type,
                    RequiredValue = achievement.RequiredValue,
                    CurrentProgress = currentProgress,
                    IsUnlocked = isUnlocked,
                    UnlockedAt = isUnlocked ? userAchievementDict[achievement.Id].UnlockedAt : null
                };

                achievementDtos.Add(achievementDto);
            }

            return achievementDtos.OrderByDescending(a => a.IsUnlocked).ThenBy(a => a.RequiredValue);
        }

        public async Task CheckAndUnlockAchievementsAsync(string userId)
        {
            var achievements = await _achievementRepository.GetAllAchievementsAsync();
            var userAchievements = await _achievementRepository.GetUserAchievementsAsync(userId);
            var unlockedAchievementIds = userAchievements.Select(ua => ua.AchievementId).ToHashSet();

            foreach (var achievement in achievements.Where(a => a.IsActive && !unlockedAchievementIds.Contains(a.Id)))
            {
                var currentProgress = await CalculateProgressAsync(userId, achievement.Type);

                if (currentProgress >= achievement.RequiredValue)
                {
                    await UnlockAchievementAsync(userId, achievement.Id, currentProgress);
                }
            }
        }

        public async Task<bool> UnlockAchievementAsync(string userId, string achievementId, int currentProgress)
        {
            var achievement = await _achievementRepository.GetAchievementByIdAsync(achievementId);
            if (achievement == null) return false;

            var existingUserAchievement = await _achievementRepository.GetUserAchievementAsync(userId, achievementId);
            if (existingUserAchievement != null) return false;

            var userAchievement = new UserAchievement
            {
                UserId = userId,
                AchievementId = achievementId,
                CurrentProgress = currentProgress,
                UnlockedAt = DateTime.UtcNow
            };

            await _achievementRepository.CreateUserAchievementAsync(userAchievement);

            await _experienceService.AddExperienceAsync(userId, achievement.RewardExperience,
                "achievement", $"Achievement unlocked: {achievement.Title}");

            _logger.LogInformation($"Achievement unlocked for user {userId}: {achievement.Title}");
            return true;
        }

        private async Task<int> CalculateProgressAsync(string userId, string achievementType)
        {
            return achievementType switch
            {
                "activity_count" => await _activityRepository.GetUserActivityCountAsync(userId),
                "food_count" => (await _foodIntakeRepository.GetByUserIdAsync(userId)).Count(),
                "level" => (await _userRepository.GetByIdAsync(userId))?.Level ?? 1,
                "referral_count" => (await _referralRepository.GetUserReferralsAsync(userId)).Count(),
                _ => 0
            };
        }
    }
}
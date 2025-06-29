using FitnessTracker.API.DTOs;
using FitnessTracker.API.Models;
using FitnessTracker.API.Repositories;
using AutoMapper;

namespace FitnessTracker.API.Services
{
    public class MissionService : IMissionService
    {
        private readonly IMissionRepository _missionRepository;
        private readonly IAchievementService _achievementService;
        private readonly IExperienceService _experienceService;
        private readonly IFoodIntakeRepository _foodIntakeRepository;
        private readonly IStepsRepository _stepsRepository;
        private readonly IBodyScanRepository _bodyScanRepository;
        private readonly IMapper _mapper;
        private readonly ILogger<MissionService> _logger;

        public MissionService(
            IMissionRepository missionRepository,
            IAchievementService achievementService,
            IExperienceService experienceService,
            IFoodIntakeRepository foodIntakeRepository,
            IStepsRepository stepsRepository,
            IBodyScanRepository bodyScanRepository,
            IMapper mapper,
            ILogger<MissionService> logger)
        {
            _missionRepository = missionRepository;
            _achievementService = achievementService;
            _experienceService = experienceService;
            _foodIntakeRepository = foodIntakeRepository;
            _stepsRepository = stepsRepository;
            _bodyScanRepository = bodyScanRepository;
            _mapper = mapper;
            _logger = logger;
        }

        public async Task<IEnumerable<MissionDto>> GetUserMissionsAsync(string userId)
        {
            var missions = await _missionRepository.GetActiveMissionsAsync();
            var userMissions = await _missionRepository.GetUserMissionsAsync(userId);
            var userMissionDict = userMissions.ToDictionary(um => um.MissionId);

            var missionDtos = new List<MissionDto>();

            foreach (var mission in missions)
            {
                var userMission = userMissionDict.GetValueOrDefault(mission.Id);

                var currentProgress = await CalculateMissionProgressAsync(userId, mission.Type, mission.TargetValue);

                var missionDto = new MissionDto
                {
                    Id = mission.Id,
                    Title = mission.Title,
                    Icon = mission.Icon,
                    RewardExperience = mission.RewardExperience,
                    Type = mission.Type,
                    TargetValue = mission.TargetValue,
                    Progress = currentProgress,
                    IsCompleted = currentProgress >= mission.TargetValue,
                    CompletedAt = userMission?.CompletedAt,
                    Route = mission.Route
                };

                missionDtos.Add(missionDto);
            }

            return missionDtos.OrderBy(m => m.IsCompleted).ThenBy(m => m.Id);
        }

        public async Task<IEnumerable<AchievementDto>> GetUserAchievementsAsync(string userId)
        {
            return await _achievementService.GetUserAchievementsAsync(userId);
        }

        public async Task UpdateMissionProgressAsync(string userId, string missionType, int incrementValue = 1)
        {
            var missions = await _missionRepository.GetActiveMissionsAsync();
            var relevantMissions = missions.Where(m => m.Type == missionType).ToList();

            foreach (var mission in relevantMissions)
            {
                var userMission = await _missionRepository.GetUserMissionAsync(userId, mission.Id);
                var actualProgress = await CalculateMissionProgressAsync(userId, mission.Type, mission.TargetValue);

                if (userMission == null)
                {
                    userMission = new UserMission
                    {
                        UserId = userId,
                        MissionId = mission.Id,
                        Progress = actualProgress
                    };

                    await _missionRepository.CreateUserMissionAsync(userMission);
                }
                else if (!userMission.IsCompleted)
                {
                    userMission.Progress = actualProgress;
                    await _missionRepository.UpdateUserMissionAsync(userMission);
                }

                // Проверяем завершение миссии
                if (!userMission.IsCompleted && actualProgress >= mission.TargetValue)
                {
                    userMission.IsCompleted = true;
                    userMission.CompletedAt = DateTime.UtcNow;
                    await _missionRepository.UpdateUserMissionAsync(userMission);

                    await _experienceService.AddExperienceAsync(userId, mission.RewardExperience,
                        "mission", $"Mission completed: {mission.Title}");

                    _logger.LogInformation($"Mission completed for user {userId}: {mission.Title}");
                }
            }

            await _achievementService.CheckAndUnlockAchievementsAsync(userId);
        }

        /// <summary>
        /// 📊 Рассчитываем актуальный прогресс миссии на основе данных пользователя
        /// </summary>
        private async Task<int> CalculateMissionProgressAsync(string userId, string missionType, int targetValue)
        {
            var today = DateTime.UtcNow.Date;

            return missionType switch
            {
                // 🔥 Миссия "Съешь 500ккал на завтрак"
                "breakfast_calories" => await CalculateBreakfastCaloriesAsync(userId, today),

                // 🚶‍♂️ Миссия "Пройди 5000 шагов" - ИСПРАВЛЕНО
                "daily_steps" => await CalculateDailyStepsAsync(userId, today),

                // 💪 Миссия "Скан тела каждую неделю"
                "weekly_body_scan" => await CalculateWeeklyBodyScanAsync(userId),

                _ => 0
            };
        }

        /// <summary>
        /// 🔥 Подсчет калорий на завтрак (6:00-11:00)
        /// </summary>
        private async Task<int> CalculateBreakfastCaloriesAsync(string userId, DateTime date)
        {
            try
            {
                var breakfastStart = date.AddHours(6); // 06:00
                var breakfastEnd = date.AddHours(11);  // 11:00

                var foodIntakes = await _foodIntakeRepository.GetByUserIdAndDateAsync(userId, date);

                var breakfastIntakes = foodIntakes.Where(f =>
                    f.DateTime >= breakfastStart && f.DateTime <= breakfastEnd);

                var totalCalories = breakfastIntakes.Sum(f =>
                    (f.NutritionPer100g.Calories * f.Weight) / 100);

                return (int)Math.Round(totalCalories);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error calculating breakfast calories for user {userId}: {ex.Message}");
                return 0;
            }
        }

        /// <summary>
        /// 🚶‍♂️ Подсчет шагов за сегодня - ИСПРАВЛЕНО
        /// </summary>
        private async Task<int> CalculateDailyStepsAsync(string userId, DateTime date)
        {
            try
            {
                // ✅ ИСПРАВЛЕНО: Используем новый метод для получения записи за конкретный день
                var todaySteps = await _stepsRepository.GetByUserIdAndDateAsync(userId, date);
                return todaySteps?.StepsCount ?? 0;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error calculating daily steps for user {userId}: {ex.Message}");
                return 0;
            }
        }

        /// <summary>
        /// 💪 Проверка сканирования тела на этой неделе
        /// </summary>
        private async Task<int> CalculateWeeklyBodyScanAsync(string userId)
        {
            try
            {
                var today = DateTime.UtcNow.Date;
                var startOfWeek = today.AddDays(-(int)today.DayOfWeek); // Начало недели (воскресенье)
                var endOfWeek = startOfWeek.AddDays(7); // Конец недели

                var bodyScans = await _bodyScanRepository.GetByUserIdAsync(userId);
                var weeklyScans = bodyScans.Where(bs =>
                    bs.ScanDate.Date >= startOfWeek && bs.ScanDate.Date < endOfWeek);

                return weeklyScans.Any() ? 1 : 0; // 1 если есть скан на этой неделе, иначе 0
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error calculating weekly body scan for user {userId}: {ex.Message}");
                return 0;
            }
        }

        /// <summary>
        /// 🎯 Обновление прогресса для специфических миссий
        /// </summary>
        public async Task CheckAndUpdateAllMissionsAsync(string userId)
        {
            // Проверяем все типы миссий
            await UpdateMissionProgressAsync(userId, "breakfast_calories");
            await UpdateMissionProgressAsync(userId, "daily_steps");
            await UpdateMissionProgressAsync(userId, "weekly_body_scan");
        }
    }
}
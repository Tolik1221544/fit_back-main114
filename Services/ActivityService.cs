using FitnessTracker.API.DTOs;
using FitnessTracker.API.Models;
using FitnessTracker.API.Repositories;
using AutoMapper;

namespace FitnessTracker.API.Services
{
    public class ActivityService : IActivityService
    {
        private readonly IActivityRepository _activityRepository;
        private readonly IStepsRepository _stepsRepository;
        private readonly IExperienceService _experienceService;
        private readonly IMapper _mapper;
        private readonly ILogger<ActivityService> _logger;

        public ActivityService(
            IActivityRepository activityRepository,
            IStepsRepository stepsRepository,
            IExperienceService experienceService,
            IMapper mapper,
            ILogger<ActivityService> logger)
        {
            _activityRepository = activityRepository;
            _stepsRepository = stepsRepository;
            _experienceService = experienceService;
            _mapper = mapper;
            _logger = logger;
        }

        public async Task<IEnumerable<ActivityDto>> GetUserActivitiesAsync(string userId, string? type = null, DateTime? startDate = null, DateTime? endDate = null)
        {
            var activities = await _activityRepository.GetByUserIdAsync(userId);

            if (!string.IsNullOrEmpty(type))
                activities = activities.Where(a => a.Type.Equals(type, StringComparison.OrdinalIgnoreCase));

            if (startDate.HasValue)
                activities = activities.Where(a => a.StartDate.Date >= startDate.Value.Date);

            if (endDate.HasValue)
                activities = activities.Where(a => a.StartDate.Date <= endDate.Value.Date);

            return _mapper.Map<IEnumerable<ActivityDto>>(activities);
        }

        public async Task<ActivityDto?> GetActivityByIdAsync(string userId, string activityId)
        {
            var activity = await _activityRepository.GetByIdAsync(activityId);
            if (activity == null || activity.UserId != userId)
                return null;

            return _mapper.Map<ActivityDto>(activity);
        }

        public async Task<ActivityDto> AddActivityAsync(string userId, AddActivityRequest request)
        {
            try
            {
                // ✅ ФИКС: Валидация обязательных полей
                if (string.IsNullOrEmpty(request.Type))
                    throw new ArgumentException("Activity type is required");

                // ✅ ФИКС: Убираем startTime и endTime, используем только Date
                var activity = new Activity
                {
                    UserId = userId,
                    Type = request.Type.ToLowerInvariant(), // Нормализация типа
                    StartDate = request.StartDate,
                    StartTime = request.StartDate, // Используем StartDate для времени
                    EndDate = request.EndDate,
                    EndTime = request.EndDate, // Используем EndDate для времени окончания
                    Calories = request.Calories ?? 0, // Значение по умолчанию
                    CreatedAt = DateTime.UtcNow
                };

                // ✅ ФИКС: Более надежная обработка данных силовой тренировки
                if (request.Type.Equals("strength", StringComparison.OrdinalIgnoreCase) && request.StrengthData != null)
                {
                    try
                    {
                        activity.StrengthData = new StrengthData
                        {
                            Name = request.StrengthData.Name ?? "Неизвестное упражнение",
                            MuscleGroup = request.StrengthData.MuscleGroup ?? "Неизвестная группа мышц",
                            Equipment = request.StrengthData.Equipment ?? "Неизвестное оборудование",
                            WorkingWeight = request.StrengthData.WorkingWeight,
                            RestTimeSeconds = request.StrengthData.RestTimeSeconds
                        };
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError($"Error setting strength data: {ex.Message}");
                        // Продолжаем без strength data если есть ошибка
                    }
                }

                // ✅ ФИКС: Более надежная обработка данных кардио тренировки
                if (request.Type.Equals("cardio", StringComparison.OrdinalIgnoreCase) && request.CardioData != null)
                {
                    try
                    {
                        activity.CardioData = new CardioData
                        {
                            CardioType = request.CardioData.CardioType ?? "Неизвестный тип кардио",
                            DistanceKm = request.CardioData.DistanceKm,
                            AvgPulse = request.CardioData.AvgPulse,
                            MaxPulse = request.CardioData.MaxPulse,
                            AvgPace = request.CardioData.AvgPace ?? ""
                        };
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError($"Error setting cardio data: {ex.Message}");
                        // Продолжаем без cardio data если есть ошибка
                    }
                }

                // ✅ ФИКС: Проверяем что активность создалась
                var createdActivity = await _activityRepository.CreateAsync(activity);
                if (createdActivity == null)
                {
                    throw new InvalidOperationException("Failed to create activity");
                }

                _logger.LogInformation($"Activity created successfully for user {userId}: {createdActivity.Id}");

                // Добавляем опыт за тренировку
                var experienceAmount = CalculateExperienceForActivity(request);
                await _experienceService.AddExperienceAsync(userId, experienceAmount, "activity",
                    $"Тренировка: {request.Type} ({experienceAmount} опыта)");

                return _mapper.Map<ActivityDto>(createdActivity);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error creating activity for user {userId}: {ex.Message}");
                throw new InvalidOperationException($"Failed to create activity: {ex.Message}");
            }
        }

        public async Task<ActivityDto> UpdateActivityAsync(string userId, string activityId, UpdateActivityRequest request)
        {
            var activity = await _activityRepository.GetByIdAsync(activityId);
            if (activity == null || activity.UserId != userId)
                throw new ArgumentException("Activity not found");

            // ✅ ФИКС: Убираем startTime и endTime
            activity.Type = request.Type?.ToLowerInvariant() ?? activity.Type;
            activity.StartDate = request.StartDate;
            activity.StartTime = request.StartDate; // Используем StartDate
            activity.EndDate = request.EndDate;
            activity.EndTime = request.EndDate; // Используем EndDate
            activity.Calories = request.Calories ?? activity.Calories;

            // Обновляем данные в зависимости от типа
            if (request.Type?.Equals("strength", StringComparison.OrdinalIgnoreCase) == true && request.StrengthData != null)
            {
                activity.StrengthData = new StrengthData
                {
                    Name = request.StrengthData.Name ?? "Неизвестное упражнение",
                    MuscleGroup = request.StrengthData.MuscleGroup ?? "Неизвестная группа мышц",
                    Equipment = request.StrengthData.Equipment ?? "Неизвестное оборудование",
                    WorkingWeight = request.StrengthData.WorkingWeight,
                    RestTimeSeconds = request.StrengthData.RestTimeSeconds
                };
                activity.CardioData = null; // Очищаем кардио данные
            }

            if (request.Type?.Equals("cardio", StringComparison.OrdinalIgnoreCase) == true && request.CardioData != null)
            {
                activity.CardioData = new CardioData
                {
                    CardioType = request.CardioData.CardioType ?? "Неизвестный тип кардио",
                    DistanceKm = request.CardioData.DistanceKm,
                    AvgPulse = request.CardioData.AvgPulse,
                    MaxPulse = request.CardioData.MaxPulse,
                    AvgPace = request.CardioData.AvgPace ?? ""
                };
                activity.StrengthData = null; // Очищаем силовые данные
            }

            var updatedActivity = await _activityRepository.UpdateAsync(activity);
            return _mapper.Map<ActivityDto>(updatedActivity);
        }

        public async Task DeleteActivityAsync(string userId, string activityId)
        {
            var activity = await _activityRepository.GetByIdAsync(activityId);
            if (activity == null || activity.UserId != userId)
                throw new ArgumentException("Activity not found");

            await _activityRepository.DeleteAsync(activityId);
        }

        public async Task<object> GetActivityStatsAsync(string userId, DateTime? startDate = null, DateTime? endDate = null)
        {
            var activities = await _activityRepository.GetByUserIdAsync(userId);

            if (startDate.HasValue)
                activities = activities.Where(a => a.CreatedAt >= startDate.Value);

            if (endDate.HasValue)
                activities = activities.Where(a => a.CreatedAt <= endDate.Value);

            var activityTypes = activities.GroupBy(a => a.Type)
                .Select(g => new { Type = g.Key, Count = g.Count() });

            var totalCalories = activities.Where(a => a.Calories.HasValue).Sum(a => a.Calories!.Value);

            return new
            {
                TotalActivities = activities.Count(),
                TotalCalories = totalCalories,
                ActivityTypes = activityTypes,
                MostPopularActivity = activityTypes.OrderByDescending(a => a.Count).FirstOrDefault()?.Type ?? "None",
                LastActivity = activities.OrderByDescending(a => a.CreatedAt).FirstOrDefault()?.CreatedAt
            };
        }

        public async Task<StepsDto> AddStepsAsync(string userId, AddStepsRequest request)
        {
            var steps = new Steps
            {
                UserId = userId,
                StepsCount = request.Steps,
                Calories = request.Calories,
                Date = request.Date
            };

            var createdSteps = await _stepsRepository.CreateAsync(steps);

            // Добавляем опыт за шаги
            var experienceAmount = CalculateExperienceForSteps(request.Steps);
            if (experienceAmount > 0)
            {
                await _experienceService.AddExperienceAsync(userId, experienceAmount, "steps",
                    $"Шаги: {request.Steps} ({experienceAmount} опыта)");
            }

            return _mapper.Map<StepsDto>(createdSteps);
        }

        public async Task<IEnumerable<StepsDto>> GetUserStepsAsync(string userId, DateTime? date = null)
        {
            var steps = await _stepsRepository.GetByUserIdAsync(userId, date);
            return _mapper.Map<IEnumerable<StepsDto>>(steps);
        }

        // Методы для расчета опыта
        private int CalculateExperienceForActivity(AddActivityRequest activity)
        {
            int baseExperience = activity.Type?.ToLowerInvariant() switch
            {
                "strength" => 25,
                "cardio" => 20,
                _ => 15
            };

            // Бонус за калории
            int calorieBonus = activity.Calories.HasValue ? (activity.Calories.Value / 100) * 5 : 0;

            // Бонус за продолжительность
            int durationBonus = 0;
            if (activity.EndDate.HasValue)
            {
                var duration = activity.EndDate.Value - activity.StartDate;
                durationBonus = Math.Min((int)duration.TotalMinutes / 10, 15);
            }

            return baseExperience + calorieBonus + durationBonus;
        }

        private int CalculateExperienceForSteps(int steps)
        {
            return steps switch
            {
                >= 15000 => 30,
                >= 10000 => 20,
                >= 7000 => 15,
                >= 5000 => 10,
                _ => 5
            };
        }
    }
}
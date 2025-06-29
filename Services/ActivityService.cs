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
        private readonly IMissionService _missionService; 
        private readonly IMapper _mapper;
        private readonly ILogger<ActivityService> _logger;

        public ActivityService(
            IActivityRepository activityRepository,
            IStepsRepository stepsRepository,
            IExperienceService experienceService,
            IMissionService missionService, 
            IMapper mapper,
            ILogger<ActivityService> logger)
        {
            _activityRepository = activityRepository;
            _stepsRepository = stepsRepository;
            _experienceService = experienceService;
            _missionService = missionService; 
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
                
                if (string.IsNullOrWhiteSpace(userId))
                    throw new ArgumentException("User ID is required");

                if (string.IsNullOrWhiteSpace(request.Type))
                    throw new ArgumentException("Activity type is required");

                if (request.StartDate == default(DateTime))
                    throw new ArgumentException("Start date is required");

                _logger.LogInformation($"Creating activity for user {userId}: type={request.Type}, startDate={request.StartDate}");

                
                var activityType = request.Type.Trim().ToLowerInvariant();
                if (activityType != "strength" && activityType != "cardio")
                    throw new ArgumentException("Activity type must be 'strength' or 'cardio'");

               
                var startDate = request.StartDate;
                var startTime = request.StartTime ?? request.StartDate;
                var endDate = request.EndDate ?? request.StartDate;
                var endTime = request.EndTime ?? request.EndDate ?? request.StartDate;

                var activity = new Activity
                {
                    Id = Guid.NewGuid().ToString(),
                    UserId = userId,
                    Type = activityType,
                    StartDate = startDate,
                    StartTime = startTime,
                    EndDate = endDate,
                    EndTime = endTime,
                    Calories = request.Calories ?? 0,
                    CreatedAt = DateTime.UtcNow
                };

                
                if (activityType == "strength" && request.StrengthData != null)
                {
                    try
                    {
                        // Валидируем обязательные поля
                        if (string.IsNullOrWhiteSpace(request.StrengthData.Name))
                            throw new ArgumentException("Strength exercise name is required");

                        activity.StrengthData = new StrengthData
                        {
                            Name = request.StrengthData.Name.Trim(),
                            MuscleGroup = request.StrengthData.MuscleGroup?.Trim() ?? "Не указано",
                            Equipment = request.StrengthData.Equipment?.Trim() ?? "Не указано",
                            WorkingWeight = Math.Max(0, request.StrengthData.WorkingWeight),
                            RestTimeSeconds = Math.Max(0, request.StrengthData.RestTimeSeconds)
                        };

                        _logger.LogInformation($"Strength data set: {activity.StrengthData.Name}, {activity.StrengthData.WorkingWeight}kg");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError($"Error setting strength data: {ex.Message}");
                        throw new ArgumentException($"Invalid strength data: {ex.Message}");
                    }
                }

               
                if (activityType == "cardio" && request.CardioData != null)
                {
                    try
                    {
                        // Валидируем обязательные поля
                        if (string.IsNullOrWhiteSpace(request.CardioData.CardioType))
                            throw new ArgumentException("Cardio type is required");

                        activity.CardioData = new CardioData
                        {
                            CardioType = request.CardioData.CardioType.Trim(),
                            DistanceKm = request.CardioData.DistanceKm.HasValue ? Math.Max(0, request.CardioData.DistanceKm.Value) : null,
                            AvgPulse = request.CardioData.AvgPulse.HasValue ? Math.Max(0, request.CardioData.AvgPulse.Value) : null,
                            MaxPulse = request.CardioData.MaxPulse.HasValue ? Math.Max(0, request.CardioData.MaxPulse.Value) : null,
                            AvgPace = request.CardioData.AvgPace?.Trim() ?? ""
                        };

                        _logger.LogInformation($"Cardio data set: {activity.CardioData.CardioType}, {activity.CardioData.DistanceKm}km");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError($"Error setting cardio data: {ex.Message}");
                        throw new ArgumentException($"Invalid cardio data: {ex.Message}");
                    }
                }

                
                if (activityType == "strength" && activity.StrengthData == null)
                {
                    _logger.LogWarning($"Strength activity without strength data for user {userId}");
                    // Создаем минимальные данные
                    activity.StrengthData = new StrengthData
                    {
                        Name = "Упражнение не указано",
                        MuscleGroup = "Не указано",
                        Equipment = "Не указано",
                        WorkingWeight = 0,
                        RestTimeSeconds = 0
                    };
                }

                if (activityType == "cardio" && activity.CardioData == null)
                {
                    _logger.LogWarning($"Cardio activity without cardio data for user {userId}");
                    // Создаем минимальные данные
                    activity.CardioData = new CardioData
                    {
                        CardioType = "Кардио не указано",
                        DistanceKm = null,
                        AvgPulse = null,
                        MaxPulse = null,
                        AvgPace = ""
                    };
                }

                _logger.LogInformation($"About to create activity in database for user {userId}");

               
                Activity createdActivity;
                try
                {
                    createdActivity = await _activityRepository.CreateAsync(activity);
                    if (createdActivity == null)
                    {
                        throw new InvalidOperationException("Repository returned null after creation");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Database error creating activity: {ex.Message}");
                    _logger.LogError($"Inner exception: {ex.InnerException?.Message}");
                    throw new InvalidOperationException($"Failed to save activity to database: {ex.Message}");
                }

                _logger.LogInformation($"Activity created successfully: {createdActivity.Id} for user {userId}");

            
                try
                {
                    var experienceAmount = CalculateExperienceForActivity(request);
                    await _experienceService.AddExperienceAsync(userId, experienceAmount, "activity",
                        $"Тренировка: {request.Type} ({experienceAmount} опыта)");

                    _logger.LogInformation($"Experience added: {experienceAmount} for user {userId}");
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Error adding experience: {ex.Message}");
                    // Не прерываем выполнение, если не удалось добавить опыт
                }

                return _mapper.Map<ActivityDto>(createdActivity);
            }
            catch (ArgumentException)
            {
                throw; // Пропускаем ошибки валидации как есть
            }
            catch (Exception ex)
            {
                _logger.LogError($"Unexpected error creating activity for user {userId}: {ex.Message}");
                _logger.LogError($"Stack trace: {ex.StackTrace}");
                throw new InvalidOperationException($"Failed to create activity: {ex.Message}");
            }
        }

        public async Task<ActivityDto> UpdateActivityAsync(string userId, string activityId, UpdateActivityRequest request)
        {
            try
            {
                var activity = await _activityRepository.GetByIdAsync(activityId);
                if (activity == null || activity.UserId != userId)
                    throw new ArgumentException("Activity not found");

                _logger.LogInformation($"Updating activity {activityId} for user {userId}");

                
                if (!string.IsNullOrWhiteSpace(request.Type))
                {
                    var activityType = request.Type.Trim().ToLowerInvariant();
                    if (activityType != "strength" && activityType != "cardio")
                        throw new ArgumentException("Activity type must be 'strength' or 'cardio'");
                    activity.Type = activityType;
                }

                if (request.StartDate != default(DateTime))
                {
                    activity.StartDate = request.StartDate;
                    activity.StartTime = request.StartTime ?? request.StartDate;
                }

                if (request.EndDate.HasValue)
                {
                    activity.EndDate = request.EndDate;
                    activity.EndTime = request.EndTime ?? request.EndDate;
                }

                if (request.Calories.HasValue)
                {
                    activity.Calories = Math.Max(0, request.Calories.Value);
                }

               
                if (activity.Type == "strength" && request.StrengthData != null)
                {
                    activity.StrengthData = new StrengthData
                    {
                        Name = !string.IsNullOrWhiteSpace(request.StrengthData.Name)
                            ? request.StrengthData.Name.Trim()
                            : activity.StrengthData?.Name ?? "Упражнение не указано",
                        MuscleGroup = request.StrengthData.MuscleGroup?.Trim() ?? activity.StrengthData?.MuscleGroup ?? "Не указано",
                        Equipment = request.StrengthData.Equipment?.Trim() ?? activity.StrengthData?.Equipment ?? "Не указано",
                        WorkingWeight = Math.Max(0, request.StrengthData.WorkingWeight),
                        RestTimeSeconds = Math.Max(0, request.StrengthData.RestTimeSeconds)
                    };
                    activity.CardioData = null; // Очищаем данные другого типа
                }

                if (activity.Type == "cardio" && request.CardioData != null)
                {
                    activity.CardioData = new CardioData
                    {
                        CardioType = !string.IsNullOrWhiteSpace(request.CardioData.CardioType)
                            ? request.CardioData.CardioType.Trim()
                            : activity.CardioData?.CardioType ?? "Кардио не указано",
                        DistanceKm = request.CardioData.DistanceKm.HasValue ? Math.Max(0, request.CardioData.DistanceKm.Value) : activity.CardioData?.DistanceKm,
                        AvgPulse = request.CardioData.AvgPulse.HasValue ? Math.Max(0, request.CardioData.AvgPulse.Value) : activity.CardioData?.AvgPulse,
                        MaxPulse = request.CardioData.MaxPulse.HasValue ? Math.Max(0, request.CardioData.MaxPulse.Value) : activity.CardioData?.MaxPulse,
                        AvgPace = request.CardioData.AvgPace?.Trim() ?? activity.CardioData?.AvgPace ?? ""
                    };
                    activity.StrengthData = null; // Очищаем данные другого типа
                }

                var updatedActivity = await _activityRepository.UpdateAsync(activity);
                _logger.LogInformation($"Activity {activityId} updated successfully");

                return _mapper.Map<ActivityDto>(updatedActivity);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error updating activity {activityId}: {ex.Message}");
                throw;
            }
        }

        public async Task DeleteActivityAsync(string userId, string activityId)
        {
            var activity = await _activityRepository.GetByIdAsync(activityId);
            if (activity == null || activity.UserId != userId)
                throw new ArgumentException("Activity not found");

            await _activityRepository.DeleteAsync(activityId);
            _logger.LogInformation($"Activity {activityId} deleted for user {userId}");
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
            // ✅ ФИКС: Валидация данных о шагах
            if (request.Steps < 0)
                throw new ArgumentException("Steps count cannot be negative");

            var steps = new Steps
            {
                Id = Guid.NewGuid().ToString(),
                UserId = userId,
                StepsCount = request.Steps,
                Calories = request.Calories.HasValue ? Math.Max(0, request.Calories.Value) : null,
                Date = request.Date,
                CreatedAt = DateTime.UtcNow
            };

            var createdSteps = await _stepsRepository.CreateAsync(steps);

            // Добавляем опыт за шаги
            var experienceAmount = CalculateExperienceForSteps(request.Steps);
            if (experienceAmount > 0)
            {
                await _experienceService.AddExperienceAsync(userId, experienceAmount, "steps",
                    $"Шаги: {request.Steps} ({experienceAmount} опыта)");
            }

            await _missionService.UpdateMissionProgressAsync(userId, "daily_steps");

            return _mapper.Map<StepsDto>(createdSteps);
        }

        public async Task<IEnumerable<StepsDto>> GetUserStepsAsync(string userId, DateTime? date = null)
        {
            var steps = await _stepsRepository.GetByUserIdAsync(userId, date);
            return _mapper.Map<IEnumerable<StepsDto>>(steps);
        }

        // ✅ ФИКС: Улучшенные методы для расчета опыта
        private int CalculateExperienceForActivity(AddActivityRequest activity)
        {
            int baseExperience = activity.Type?.ToLowerInvariant() switch
            {
                "strength" => 25,
                "cardio" => 20,
                _ => 15
            };

            // Бонус за калории
            int calorieBonus = activity.Calories.HasValue ? Math.Min(20, (activity.Calories.Value / 100) * 5) : 0;

            // Бонус за продолжительность
            int durationBonus = 0;
            var endTime = activity.EndDate ?? activity.EndTime;
            var startTime = activity.StartDate != default ? activity.StartDate : (activity.StartTime ?? DateTime.UtcNow);

            if (endTime.HasValue)
            {
                var duration = endTime.Value - startTime;
                durationBonus = Math.Min(15, (int)duration.TotalMinutes / 10);
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
                >= 1000 => 5,
                _ => 0
            };
        }
    }
}
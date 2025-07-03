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
                    if (string.IsNullOrWhiteSpace(request.StrengthData.Name))
                        throw new ArgumentException("Strength exercise name is required");

                    var sets = new List<StrengthSet>();
                    if (request.StrengthData.Sets?.Any() == true)
                    {
                        // Используем подходы из запроса
                        sets = request.StrengthData.Sets.Select((setDto, index) => new StrengthSet
                        {
                            SetNumber = setDto.SetNumber > 0 ? setDto.SetNumber : index + 1,
                            Weight = Math.Max(0, setDto.Weight),
                            Reps = Math.Max(0, setDto.Reps),
                            IsCompleted = setDto.IsCompleted,
                            Notes = setDto.Notes?.Trim()
                        }).ToList();
                    }
                    else if (request.StrengthData.WorkingWeight > 0)
                    {
                        // Создаем один подход на основе старых данных для совместимости
                        sets.Add(new StrengthSet
                        {
                            SetNumber = 1,
                            Weight = request.StrengthData.WorkingWeight,
                            Reps = 10, // Дефолтное значение
                            IsCompleted = true,
                            Notes = "Автоматически созданный подход"
                        });
                    }

                    activity.StrengthData = new StrengthData
                    {
                        Name = request.StrengthData.Name.Trim(),
                        MuscleGroup = request.StrengthData.MuscleGroup?.Trim() ?? "Не указано",
                        Equipment = request.StrengthData.Equipment?.Trim() ?? "Не указано",
                        WorkingWeight = sets.Any() ? sets.Average(s => s.Weight) : request.StrengthData.WorkingWeight,
                        RestTimeSeconds = Math.Max(0, request.StrengthData.RestTimeSeconds),
                        Sets = sets
                    };

                    // Явно устанавливаем CardioData в null
                    activity.CardioData = null;
                }

                if (activityType == "cardio" && request.CardioData != null)
                {
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
                    // Явно устанавливаем StrengthData в null
                    activity.StrengthData = null;
                }

                // Создаем дефолтные данные если они отсутствуют
                if (activityType == "strength" && activity.StrengthData == null)
                {
                    activity.StrengthData = new StrengthData
                    {
                        Name = "Упражнение не указано",
                        MuscleGroup = "Не указано",
                        Equipment = "Не указано",
                        WorkingWeight = 0,
                        RestTimeSeconds = 0,
                        Sets = new List<StrengthSet>
                {
                    new StrengthSet
                    {
                        SetNumber = 1,
                        Weight = 0,
                        Reps = 0,
                        IsCompleted = false,
                        Notes = "Данные не указаны"
                    }
                }
                    };
                }

                if (activityType == "cardio" && activity.CardioData == null)
                {
                    activity.CardioData = new CardioData
                    {
                        CardioType = "Кардио не указано",
                        DistanceKm = null,
                        AvgPulse = null,
                        MaxPulse = null,
                        AvgPace = ""
                    };
                }

                var createdActivity = await _activityRepository.CreateAsync(activity);

                try
                {
                    var experienceAmount = CalculateExperienceForActivity(request);
                    await _experienceService.AddExperienceAsync(userId, experienceAmount, "activity",
                        $"Тренировка: {request.Type} ({experienceAmount} опыта)");
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Error adding experience: {ex.Message}");
                }

                return _mapper.Map<ActivityDto>(createdActivity);
            }
            catch (ArgumentException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Unexpected error creating activity for user {userId}: {ex.Message}");
                throw new InvalidOperationException($"Failed to create activity: {ex.Message}");
            }
        }

        public async Task<ActivityDto> UpdateActivityAsync(string userId, string activityId, UpdateActivityRequest request)
        {
            var activity = await _activityRepository.GetByIdAsync(activityId);
            if (activity == null || activity.UserId != userId)
                throw new ArgumentException("Activity not found");

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
                var sets = new List<StrengthSet>();
                if (request.StrengthData.Sets?.Any() == true)
                {
                    sets = request.StrengthData.Sets.Select((setDto, index) => new StrengthSet
                    {
                        SetNumber = setDto.SetNumber > 0 ? setDto.SetNumber : index + 1,
                        Weight = Math.Max(0, setDto.Weight),
                        Reps = Math.Max(0, setDto.Reps),
                        IsCompleted = setDto.IsCompleted,
                        Notes = setDto.Notes?.Trim()
                    }).ToList();
                }
                else
                {
                    // Сохраняем существующие подходы или создаем новый
                    sets = activity.StrengthData?.Sets ?? new List<StrengthSet>();
                    if (!sets.Any() && request.StrengthData.WorkingWeight > 0)
                    {
                        sets.Add(new StrengthSet
                        {
                            SetNumber = 1,
                            Weight = request.StrengthData.WorkingWeight,
                            Reps = 10,
                            IsCompleted = true,
                            Notes = "Обновленный подход"
                        });
                    }
                }

                activity.StrengthData = new StrengthData
                {
                    Name = !string.IsNullOrWhiteSpace(request.StrengthData.Name)
                        ? request.StrengthData.Name.Trim()
                        : activity.StrengthData?.Name ?? "Упражнение не указано",
                    MuscleGroup = request.StrengthData.MuscleGroup?.Trim() ?? activity.StrengthData?.MuscleGroup ?? "Не указано",
                    Equipment = request.StrengthData.Equipment?.Trim() ?? activity.StrengthData?.Equipment ?? "Не указано",
                    WorkingWeight = sets.Any() ? sets.Average(s => s.Weight) : Math.Max(0, request.StrengthData.WorkingWeight),
                    RestTimeSeconds = Math.Max(0, request.StrengthData.RestTimeSeconds),
                    Sets = sets
                };
                activity.CardioData = null;
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
                activity.StrengthData = null;
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

            var activityCalories = activities.Where(a => a.Calories.HasValue).Sum(a => a.Calories!.Value);

            // Получаем калории от шагов за тот же период
            var stepsCalories = await GetStepsCaloriesForPeriodAsync(userId, startDate, endDate);

            var totalCalories = activityCalories + stepsCalories;

            return new
            {
                TotalActivities = activities.Count(),
                TotalCalories = totalCalories,
                ActivityCalories = activityCalories,
                StepsCalories = stepsCalories,
                ActivityTypes = activityTypes,
                MostPopularActivity = activityTypes.OrderByDescending(a => a.Count).FirstOrDefault()?.Type ?? "None",
                LastActivity = activities.OrderByDescending(a => a.CreatedAt).FirstOrDefault()?.CreatedAt
            };
        }

        public async Task<StepsDto> AddStepsAsync(string userId, AddStepsRequest request)
        {
            try
            {
                if (request.Steps < 0)
                    throw new ArgumentException("Steps count cannot be negative");

                if (string.IsNullOrEmpty(userId))
                    throw new ArgumentException("User ID is required");

                var targetDate = request.Date.Date;
                _logger.LogInformation($"Adding/updating steps for user {userId} on {targetDate:yyyy-MM-dd}: {request.Steps} steps");

                // Ищем существующую запись за этот день
                var existingSteps = await _stepsRepository.GetByUserIdAndDateAsync(userId, targetDate);

                Steps stepsRecord;

                if (existingSteps != null)
                {
                    // Обновляем существующую запись
                    existingSteps.StepsCount = request.Steps;
                    existingSteps.Calories = request.Calories.HasValue ? Math.Max(0, request.Calories.Value) : null;
                    stepsRecord = await _stepsRepository.UpdateAsync(existingSteps);

                    _logger.LogInformation($"Updated steps for user {userId} on {targetDate:yyyy-MM-dd}: {request.Steps} steps");
                }
                else
                {
                    // Создаем новую запись
                    stepsRecord = new Steps
                    {
                        Id = Guid.NewGuid().ToString(),
                        UserId = userId,
                        StepsCount = request.Steps,
                        Calories = request.Calories.HasValue ? Math.Max(0, request.Calories.Value) : null,
                        Date = targetDate,
                        CreatedAt = DateTime.UtcNow
                    };

                    stepsRecord = await _stepsRepository.CreateAsync(stepsRecord);
                    _logger.LogInformation($"Created new steps record for user {userId} on {targetDate:yyyy-MM-dd}: {request.Steps} steps");
                }

                // Добавляем опыт за шаги
                try
                {
                    var experienceAmount = CalculateExperienceForSteps(request.Steps);
                    if (experienceAmount > 0)
                    {
                        await _experienceService.AddExperienceAsync(userId, experienceAmount, "steps",
                            $"Шаги: {request.Steps} ({experienceAmount} опыта)");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Error adding experience for steps: {ex.Message}");
                    // Не прерываем выполнение, если не удалось добавить опыт
                }

                // Обновляем прогресс миссий
                try
                {
                    await _missionService.UpdateMissionProgressAsync(userId, "daily_steps");
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Error updating mission progress: {ex.Message}");
                    // Не прерываем выполнение
                }

                return _mapper.Map<StepsDto>(stepsRecord);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error adding/updating steps for user {userId}: {ex.Message}");
                _logger.LogError($"Stack trace: {ex.StackTrace}");
                throw;
            }
        }

        public async Task<IEnumerable<StepsDto>> GetUserStepsAsync(string userId, DateTime? date = null)
        {
            IEnumerable<Steps> steps;

            if (date.HasValue)
            {
                // Получаем шаги за конкретный день
                var targetDate = date.Value.Date;
                var singleSteps = await _stepsRepository.GetByUserIdAndDateAsync(userId, targetDate);
                steps = singleSteps != null ? new[] { singleSteps } : Array.Empty<Steps>();
            }
            else
            {
                // Получаем все шаги (по одной записи на день)
                steps = await _stepsRepository.GetByUserIdAsync(userId);
            }

            return _mapper.Map<IEnumerable<StepsDto>>(steps);
        }


        private async Task<int> GetStepsCaloriesForPeriodAsync(string userId, DateTime? startDate, DateTime? endDate)
        {
            var allSteps = await _stepsRepository.GetByUserIdAsync(userId);

            if (startDate.HasValue)
                allSteps = allSteps.Where(s => s.Date >= startDate.Value.Date);

            if (endDate.HasValue)
                allSteps = allSteps.Where(s => s.Date <= endDate.Value.Date);

            return allSteps.Where(s => s.Calories.HasValue).Sum(s => s.Calories!.Value);
        }

        private int CalculateStepsStreak(IEnumerable<Steps> orderedSteps)
        {
            var steps = orderedSteps.ToList();
            if (!steps.Any()) return 0;

            int streak = 0;
            var today = DateTime.UtcNow.Date;

            foreach (var step in steps)
            {
                var expectedDate = today.AddDays(-streak);
                if (step.Date.Date == expectedDate && step.StepsCount >= 1000) // Минимум 1000 шагов для streak
                {
                    streak++;
                }
                else
                {
                    break;
                }
            }

            return streak;
        }

        private decimal GetCaloriesPerDay(IEnumerable<Activity> activities, IEnumerable<Steps> steps, DateTime? startDate, DateTime? endDate)
        {
            var totalDays = 1;

            if (startDate.HasValue && endDate.HasValue)
            {
                totalDays = Math.Max(1, (endDate.Value - startDate.Value).Days + 1);
            }
            else
            {
                var activityDays = activities.Select(a => a.CreatedAt.Date).Distinct().Count();
                var stepsDays = steps.Select(s => s.Date.Date).Distinct().Count();
                totalDays = Math.Max(1, Math.Max(activityDays, stepsDays));
            }

            var activityCalories = activities.Where(a => a.Calories.HasValue).Sum(a => a.Calories!.Value);
            var stepsCalories = steps.Where(s => s.Calories.HasValue).Sum(s => s.Calories!.Value);

            return Math.Round((decimal)(activityCalories + stepsCalories) / totalDays, 1);
        }

        private decimal GetWorkoutsPerWeek(IEnumerable<Activity> activities, DateTime? startDate, DateTime? endDate)
        {
            if (!activities.Any()) return 0;

            var totalWeeks = 1m;

            if (startDate.HasValue && endDate.HasValue)
            {
                totalWeeks = Math.Max(1, (decimal)(endDate.Value - startDate.Value).TotalDays / 7);
            }
            else
            {
                var firstActivity = activities.Min(a => a.CreatedAt);
                var lastActivity = activities.Max(a => a.CreatedAt);
                totalWeeks = Math.Max(1, (decimal)(lastActivity - firstActivity).TotalDays / 7);
            }

            return Math.Round(activities.Count() / totalWeeks, 1);
        }

        private decimal GetStepsPerWeek(IEnumerable<Steps> steps, DateTime? startDate, DateTime? endDate)
        {
            if (!steps.Any()) return 0;

            var totalWeeks = 1m;

            if (startDate.HasValue && endDate.HasValue)
            {
                totalWeeks = Math.Max(1, (decimal)(endDate.Value - startDate.Value).TotalDays / 7);
            }
            else
            {
                var firstStep = steps.Min(s => s.Date);
                var lastStep = steps.Max(s => s.Date);
                totalWeeks = Math.Max(1, (decimal)(lastStep - firstStep).TotalDays / 7);
            }

            return Math.Round(steps.Sum(s => s.StepsCount) / totalWeeks, 0);
        }

        private int CalculateExperienceForActivity(AddActivityRequest activity)
        {
            int baseExperience = activity.Type?.ToLowerInvariant() switch
            {
                "strength" => 25,
                "cardio" => 20,
                _ => 15
            };

            int calorieBonus = activity.Calories.HasValue ? Math.Min(20, (activity.Calories.Value / 100) * 5) : 0;

            int durationBonus = 0;
            var endTime = activity.EndDate ?? activity.EndTime;
            var startTime = activity.StartDate != default ? activity.StartDate : (activity.StartTime ?? DateTime.UtcNow);

            if (endTime.HasValue)
            {
                var duration = endTime.Value - startTime;
                durationBonus = Math.Min(15, (int)duration.TotalMinutes / 10);
            }

            int setsBonus = 0;
            if (activity.Type?.ToLowerInvariant() == "strength" && activity.StrengthData?.Sets?.Any() == true)
            {
                var setsCount = activity.StrengthData.Sets.Count;
                var totalReps = activity.StrengthData.Sets.Sum(s => s.Reps);

                setsBonus = Math.Min(25, setsCount * 3); // 3 опыта за подход, максимум 25

                // Дополнительный бонус за большое количество повторений
                if (totalReps > 50)
                    setsBonus += 10;
                else if (totalReps > 30)
                    setsBonus += 5;
            }

            return baseExperience + calorieBonus + durationBonus + setsBonus;
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
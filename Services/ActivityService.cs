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
        private readonly IUserRepository _userRepository;
        private readonly IMapper _mapper;
        private readonly ILogger<ActivityService> _logger;

        public ActivityService(
            IActivityRepository activityRepository,
            IStepsRepository stepsRepository,
            IExperienceService experienceService,
            IMissionService missionService,
            IUserRepository userRepository,
            IMapper mapper,
            ILogger<ActivityService> logger)
        {
            _activityRepository = activityRepository;
            _stepsRepository = stepsRepository;
            _experienceService = experienceService;
            _missionService = missionService;
            _userRepository = userRepository;
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

                var activityType = request.Type.Trim().ToLowerInvariant();
                if (activityType != "strength" && activityType != "cardio")
                    throw new ArgumentException("Activity type must be 'strength' or 'cardio'");

                var user = await _userRepository.GetByIdAsync(userId);
                var userTimeZone = GetTimeZoneFromLocale(user?.Locale);

                var startDate = ConvertToUtc(request.StartDate, userTimeZone);
                var endDate = request.EndDate.HasValue ?
                    ConvertToUtc(request.EndDate.Value, userTimeZone) :
                    startDate.AddMinutes(30);

                var activity = new Activity
                {
                    Id = Guid.NewGuid().ToString(),
                    UserId = userId,
                    Type = activityType,
                    StartDate = startDate,
                    EndDate = endDate,
                    Calories = request.Calories ?? CalculateCalories(request),
                    CreatedAt = DateTime.UtcNow
                };

                if (request.ActivityData != null)
                {
                    if (string.IsNullOrWhiteSpace(request.ActivityData.Name))
                        throw new ArgumentException("Activity name is required");

                    var activityData = new ActivityData
                    {
                        Name = request.ActivityData.Name.Trim(),
                        Category = request.ActivityData.Category?.Trim(),
                        Equipment = string.IsNullOrWhiteSpace(request.ActivityData.Equipment) ? null : request.ActivityData.Equipment.Trim()
                    };

                    if (activityType == "strength")
                    {
                        activityData.MuscleGroup = string.IsNullOrWhiteSpace(request.ActivityData.MuscleGroup) ?
                            null : request.ActivityData.MuscleGroup.Trim();

                        activityData.Weight = request.ActivityData.Weight > 0 ? request.ActivityData.Weight : null;
                        activityData.RestTimeSeconds = request.ActivityData.RestTimeSeconds > 0 ? request.ActivityData.RestTimeSeconds : null;

                        if (request.ActivityData.Sets?.Any() == true)
                        {
                            var validSets = new List<ActivitySet>();
                            int totalReps = 0;

                            foreach (var setDto in request.ActivityData.Sets)
                            {
                                if (setDto.Reps <= 0) continue;

                                validSets.Add(new ActivitySet
                                {
                                    SetNumber = setDto.SetNumber > 0 ? setDto.SetNumber : validSets.Count + 1,
                                    Weight = setDto.Weight > 0 ? setDto.Weight : null,
                                    Reps = setDto.Reps,
                                    IsCompleted = setDto.IsCompleted
                                });

                                totalReps += setDto.Reps;
                            }

                            activityData.Sets = validSets;
                            activityData.Count = totalReps;
                        }
                        else
                        {
                            if (request.ActivityData.Count > 0)
                            {
                                activityData.Sets = new List<ActivitySet>
                                {
                                    new ActivitySet
                                    {
                                        SetNumber = 1,
                                        Weight = request.ActivityData.Weight > 0 ? request.ActivityData.Weight : null,
                                        Reps = request.ActivityData.Count.Value,
                                        IsCompleted = true
                                    }
                                };
                                activityData.Count = request.ActivityData.Count;
                            }
                        }
                    }
                    else if (activityType == "cardio")
                    {
                        activityData.Distance = request.ActivityData.Distance > 0 ? request.ActivityData.Distance : null;
                        activityData.AvgPace = string.IsNullOrWhiteSpace(request.ActivityData.AvgPace) ? null : request.ActivityData.AvgPace.Trim();
                        activityData.AvgPulse = request.ActivityData.AvgPulse > 0 ? request.ActivityData.AvgPulse : null;
                        activityData.MaxPulse = request.ActivityData.MaxPulse > 0 ? request.ActivityData.MaxPulse : null;
                        activityData.Count = request.ActivityData.Count > 0 ? request.ActivityData.Count : null;
                    }

                    activity.ActivityData = activityData;
                }

                var createdActivity = await _activityRepository.CreateAsync(activity);

                var experienceAmount = CalculateExperienceForActivity(activity);
                await _experienceService.AddExperienceAsync(userId, experienceAmount, "activity",
                    $"Тренировка: {activity.Type} ({experienceAmount} опыта)");

                var activityDto = _mapper.Map<ActivityDto>(createdActivity);
                activityDto.StartDate = ConvertFromUtc(createdActivity.StartDate, userTimeZone);
                if (createdActivity.EndDate.HasValue)
                    activityDto.EndDate = ConvertFromUtc(createdActivity.EndDate.Value, userTimeZone);

                return activityDto;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error creating activity: {ex.Message}");
                throw;
            }
        }

        public async Task<ActivityDto> UpdateActivityAsync(string userId, string activityId, UpdateActivityRequest request)
        {
            var activity = await _activityRepository.GetByIdAsync(activityId);
            if (activity == null || activity.UserId != userId)
                throw new ArgumentException("Activity not found");

            var user = await _userRepository.GetByIdAsync(userId);
            var userTimeZone = GetTimeZoneFromLocale(user?.Locale);

            if (!string.IsNullOrWhiteSpace(request.Type))
            {
                var activityType = request.Type.Trim().ToLowerInvariant();
                if (activityType != "strength" && activityType != "cardio")
                    throw new ArgumentException("Activity type must be 'strength' or 'cardio'");
                activity.Type = activityType;
            }

            if (request.StartDate != default(DateTime))
            {
                activity.StartDate = ConvertToUtc(request.StartDate, userTimeZone);
            }

            if (request.EndDate.HasValue)
            {
                activity.EndDate = ConvertToUtc(request.EndDate.Value, userTimeZone);
            }

            if (request.Calories.HasValue)
            {
                activity.Calories = Math.Max(0, request.Calories.Value);
            }

            if (request.ActivityData != null)
            {
                var activityData = new ActivityData
                {
                    Name = !string.IsNullOrWhiteSpace(request.ActivityData.Name)
                        ? request.ActivityData.Name.Trim()
                        : activity.ActivityData?.Name ?? "Упражнение",
                    Category = string.IsNullOrWhiteSpace(request.ActivityData.Category) ? null : request.ActivityData.Category.Trim(),
                    Equipment = string.IsNullOrWhiteSpace(request.ActivityData.Equipment) ? null : request.ActivityData.Equipment.Trim()
                };

                if (activity.Type == "strength")
                {
                    activityData.MuscleGroup = string.IsNullOrWhiteSpace(request.ActivityData.MuscleGroup) ?
                        null : request.ActivityData.MuscleGroup.Trim();

                    activityData.Weight = request.ActivityData.Weight > 0 ? request.ActivityData.Weight : null;
                    activityData.RestTimeSeconds = request.ActivityData.RestTimeSeconds > 0 ? request.ActivityData.RestTimeSeconds : null;

                    if (request.ActivityData.Sets?.Any() == true)
                    {
                        var validSets = new List<ActivitySet>();
                        int totalReps = 0;

                        foreach (var setDto in request.ActivityData.Sets)
                        {
                            if (setDto.Reps <= 0) continue;

                            validSets.Add(new ActivitySet
                            {
                                SetNumber = setDto.SetNumber > 0 ? setDto.SetNumber : validSets.Count + 1,
                                Weight = setDto.Weight > 0 ? setDto.Weight : null,
                                Reps = setDto.Reps,
                                IsCompleted = setDto.IsCompleted
                            });

                            totalReps += setDto.Reps;
                        }

                        activityData.Sets = validSets;
                        activityData.Count = totalReps;
                    }
                    else
                    {
                        activityData.Sets = activity.ActivityData?.Sets;
                        activityData.Count = activity.ActivityData?.Count;
                    }
                }
                else if (activity.Type == "cardio")
                {
                    activityData.Distance = request.ActivityData.Distance > 0 ? request.ActivityData.Distance : null;
                    activityData.AvgPace = string.IsNullOrWhiteSpace(request.ActivityData.AvgPace) ? null : request.ActivityData.AvgPace.Trim();
                    activityData.AvgPulse = request.ActivityData.AvgPulse > 0 ? request.ActivityData.AvgPulse : null;
                    activityData.MaxPulse = request.ActivityData.MaxPulse > 0 ? request.ActivityData.MaxPulse : null;
                    activityData.Count = request.ActivityData.Count > 0 ? request.ActivityData.Count : null;
                }

                activity.ActivityData = activityData;
            }

            var updatedActivity = await _activityRepository.UpdateAsync(activity);

            var activityDto = _mapper.Map<ActivityDto>(updatedActivity);
            activityDto.StartDate = ConvertFromUtc(updatedActivity.StartDate, userTimeZone);
            if (updatedActivity.EndDate.HasValue)
                activityDto.EndDate = ConvertFromUtc(updatedActivity.EndDate.Value, userTimeZone);

            return activityDto;
        }

        private TimeZoneInfo GetTimeZoneFromLocale(string? locale)
        {
            if (string.IsNullOrEmpty(locale))
                return TimeZoneInfo.Utc;

            var timeZoneMap = new Dictionary<string, string>
            {
                // Россия
                ["ru_RU"] = "Russian Standard Time", // UTC+3 (Москва)
                ["ru"] = "Russian Standard Time",

                // США
                ["en_US"] = "Eastern Standard Time", // UTC-5
                ["en"] = "UTC",

                // Европа
                ["en_GB"] = "GMT Standard Time", // UTC+0
                ["de_DE"] = "W. Europe Standard Time", // UTC+1
                ["fr_FR"] = "Romance Standard Time", // UTC+1
                ["es_ES"] = "Romance Standard Time", // UTC+1
                ["it_IT"] = "W. Europe Standard Time", // UTC+1

                // Азия
                ["zh_CN"] = "China Standard Time", // UTC+8
                ["ja_JP"] = "Tokyo Standard Time", // UTC+9
                ["ko_KR"] = "Korea Standard Time", // UTC+9

                // Латинская Америка
                ["es_MX"] = "Central Standard Time (Mexico)", // UTC-6
                ["pt_BR"] = "E. South America Standard Time", // UTC-3

                // Другие
                ["ar_SA"] = "Arab Standard Time", // UTC+3
                ["hi_IN"] = "India Standard Time", // UTC+5:30
                ["tr_TR"] = "Turkey Standard Time", // UTC+3
            };

            try
            {
                var localeKey = locale.ToLower();

                // Пробуем точное совпадение
                if (timeZoneMap.ContainsKey(localeKey))
                {
                    var tzId = timeZoneMap[localeKey];
                    return TimeZoneInfo.FindSystemTimeZoneById(tzId);
                }

                // Пробуем по языковому коду (первые 2 символа)
                var langCode = localeKey.Length >= 2 ? localeKey.Substring(0, 2) : localeKey;
                if (timeZoneMap.ContainsKey(langCode))
                {
                    var tzId = timeZoneMap[langCode];
                    return TimeZoneInfo.FindSystemTimeZoneById(tzId);
                }

                _logger.LogWarning($"TimeZone not found for locale: {locale}, using UTC");
                return TimeZoneInfo.Utc;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error getting timezone for locale {locale}: {ex.Message}");
                return TimeZoneInfo.Utc;
            }
        }

        private DateTime ConvertToUtc(DateTime dateTime, TimeZoneInfo timeZone)
        {
            if (dateTime.Kind == DateTimeKind.Utc)
                return dateTime;

            try
            {
                return TimeZoneInfo.ConvertTimeToUtc(dateTime, timeZone);
            }
            catch
            {
                return dateTime;
            }
        }

        private DateTime ConvertFromUtc(DateTime utcDateTime, TimeZoneInfo timeZone)
        {
            try
            {
                return TimeZoneInfo.ConvertTimeFromUtc(utcDateTime, timeZone);
            }
            catch
            {
                return utcDateTime;
            }
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

                var targetDate = request.Date.Date;
                var existingSteps = await _stepsRepository.GetByUserIdAndDateAsync(userId, targetDate);

                Steps stepsRecord;

                if (existingSteps != null)
                {
                    existingSteps.StepsCount = request.Steps;
                    existingSteps.Calories = request.Calories;
                    stepsRecord = await _stepsRepository.UpdateAsync(existingSteps);
                }
                else
                {
                    stepsRecord = new Steps
                    {
                        Id = Guid.NewGuid().ToString(),
                        UserId = userId,
                        StepsCount = request.Steps,
                        Calories = request.Calories,
                        Date = targetDate,
                        CreatedAt = DateTime.UtcNow
                    };

                    stepsRecord = await _stepsRepository.CreateAsync(stepsRecord);
                }

                var experienceAmount = CalculateExperienceForSteps(request.Steps);
                if (experienceAmount > 0)
                {
                    await _experienceService.AddExperienceAsync(userId, experienceAmount, "steps",
                        $"Шаги: {request.Steps} ({experienceAmount} опыта)");
                }

                await _missionService.UpdateMissionProgressAsync(userId, "daily_steps");

                return _mapper.Map<StepsDto>(stepsRecord);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error adding/updating steps: {ex.Message}");
                throw;
            }
        }

        public async Task<IEnumerable<StepsDto>> GetUserStepsAsync(string userId, DateTime? date = null)
        {
            IEnumerable<Steps> steps;

            if (date.HasValue)
            {
                var targetDate = date.Value.Date;
                var singleSteps = await _stepsRepository.GetByUserIdAndDateAsync(userId, targetDate);
                steps = singleSteps != null ? new[] { singleSteps } : Array.Empty<Steps>();
            }
            else
            {
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

        private int CalculateCalories(AddActivityRequest request)
        {
            if (request.Calories.HasValue)
                return request.Calories.Value;

            var duration = (request.EndDate ?? request.StartDate.AddMinutes(30)) - request.StartDate;
            var minutes = (int)duration.TotalMinutes;

            return request.Type?.ToLowerInvariant() == "cardio" ? minutes * 8 : minutes * 5;
        }

        private int CalculateExperienceForActivity(Activity activity)
        {
            int baseExperience = activity.Type == "strength" ? 25 : 20;

            var duration = activity.EndDate - activity.StartDate;
            int durationBonus = Math.Min(20, (int)(duration?.TotalMinutes ?? 0) / 10);

            int setsBonus = 0;
            if (activity.Type == "strength" && activity.ActivityData?.Sets?.Any() == true)
            {
                setsBonus = Math.Min(25, activity.ActivityData.Sets.Count * 5);
            }

            int distanceBonus = 0;
            if (activity.Type == "cardio" && activity.ActivityData?.Distance > 0)
            {
                distanceBonus = Math.Min(30, (int)(activity.ActivityData.Distance.Value * 5));
            }

            return baseExperience + durationBonus + setsBonus + distanceBonus;
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
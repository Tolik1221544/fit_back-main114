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

                var activityType = request.Type.Trim().ToLowerInvariant();
                if (activityType != "strength" && activityType != "cardio")
                    throw new ArgumentException("Activity type must be 'strength' or 'cardio'");

                var activity = new Activity
                {
                    Id = Guid.NewGuid().ToString(),
                    UserId = userId,
                    Type = activityType,
                    StartDate = request.StartDate,
                    StartTime = request.StartTime ?? request.StartDate,
                    EndDate = request.EndDate ?? request.StartDate,
                    EndTime = request.EndTime ?? request.EndDate ?? request.StartDate.AddMinutes(30),
                    Calories = request.Calories ?? CalculateCalories(request),
                    CreatedAt = DateTime.UtcNow
                };

                if (request.ActivityData != null)
                {
                    var activityData = new ActivityData
                    {
                        Name = request.ActivityData.Name ?? "Упражнение",
                        Category = request.ActivityData.Category,
                        Equipment = request.ActivityData.Equipment ?? "Нет",
                        Count = request.ActivityData.Count
                    };

                    if (activityType == "strength")
                    {
                        activityData.MuscleGroup = request.ActivityData.MuscleGroup ?? "Общая группа";
                        activityData.Weight = request.ActivityData.Weight;
                        activityData.RestTimeSeconds = request.ActivityData.RestTimeSeconds ?? 90;

                        if (request.ActivityData.Sets?.Any() == true)
                        {
                            activityData.Sets = request.ActivityData.Sets.Select((s, i) => new ActivitySet
                            {
                                SetNumber = s.SetNumber > 0 ? s.SetNumber : i + 1,
                                Weight = s.Weight,
                                Reps = s.Reps,
                                IsCompleted = s.IsCompleted
                            }).ToList();
                        }
                    }
                    else if (activityType == "cardio")
                    {
                        activityData.Distance = request.ActivityData.Distance;
                        activityData.AvgPace = request.ActivityData.AvgPace;
                        activityData.AvgPulse = request.ActivityData.AvgPulse;
                        activityData.MaxPulse = request.ActivityData.MaxPulse;
                    }

                    activity.ActivityData = activityData;
                }

                else if (activityType == "strength" && request.StrengthData != null)
                {
                    activity.StrengthData = new StrengthData
                    {
                        Name = request.StrengthData.Name,
                        MuscleGroup = request.StrengthData.MuscleGroup,
                        Equipment = request.StrengthData.Equipment,
                        WorkingWeight = request.StrengthData.WorkingWeight,
                        RestTimeSeconds = request.StrengthData.RestTimeSeconds,
                        Sets = request.StrengthData.Sets?.Select(s => new StrengthSet
                        {
                            SetNumber = s.SetNumber,
                            Weight = s.Weight,
                            Reps = s.Reps,
                            IsCompleted = s.IsCompleted,
                            Notes = s.Notes
                        }).ToList() ?? new List<StrengthSet>()
                    };

                    activity.ActivityData = new ActivityData
                    {
                        Name = request.StrengthData.Name,
                        MuscleGroup = request.StrengthData.MuscleGroup,
                        Equipment = request.StrengthData.Equipment,
                        Weight = request.StrengthData.WorkingWeight,
                        RestTimeSeconds = request.StrengthData.RestTimeSeconds,
                        Sets = request.StrengthData.Sets?.Select(s => new ActivitySet
                        {
                            SetNumber = s.SetNumber,
                            Weight = s.Weight,
                            Reps = s.Reps,
                            IsCompleted = s.IsCompleted
                        }).ToList(),
                        Count = request.StrengthData.TotalReps
                    };
                }
                else if (activityType == "cardio" && request.CardioData != null)
                {
                    activity.CardioData = new CardioData
                    {
                        CardioType = request.CardioData.CardioType,
                        DistanceKm = request.CardioData.DistanceKm,
                        AvgPulse = request.CardioData.AvgPulse,
                        MaxPulse = request.CardioData.MaxPulse,
                        AvgPace = request.CardioData.AvgPace
                    };

                    activity.ActivityData = new ActivityData
                    {
                        Name = request.CardioData.CardioType,
                        Distance = request.CardioData.DistanceKm,
                        AvgPace = request.CardioData.AvgPace,
                        AvgPulse = request.CardioData.AvgPulse,
                        MaxPulse = request.CardioData.MaxPulse,
                        Count = request.CardioData.JumpRopeData?.JumpCount
                    };
                }

                var createdActivity = await _activityRepository.CreateAsync(activity);

                var experienceAmount = CalculateExperienceForActivity(activity);
                await _experienceService.AddExperienceAsync(userId, experienceAmount, "activity",
                    $"Тренировка: {activity.Type} ({experienceAmount} опыта)");

                return _mapper.Map<ActivityDto>(createdActivity);
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

            if (request.ActivityData != null)
            {
                var activityData = new ActivityData
                {
                    Name = request.ActivityData.Name ?? activity.ActivityData?.Name ?? "Упражнение",
                    Category = request.ActivityData.Category ?? activity.ActivityData?.Category,
                    Equipment = request.ActivityData.Equipment ?? activity.ActivityData?.Equipment ?? "Нет",
                    Count = request.ActivityData.Count ?? activity.ActivityData?.Count
                };

                if (activity.Type == "strength")
                {
                    activityData.MuscleGroup = request.ActivityData.MuscleGroup ?? activity.ActivityData?.MuscleGroup ?? "Общая группа";
                    activityData.Weight = request.ActivityData.Weight ?? activity.ActivityData?.Weight;
                    activityData.RestTimeSeconds = request.ActivityData.RestTimeSeconds ?? activity.ActivityData?.RestTimeSeconds ?? 90;

                    if (request.ActivityData.Sets?.Any() == true)
                    {
                        activityData.Sets = request.ActivityData.Sets.Select((s, i) => new ActivitySet
                        {
                            SetNumber = s.SetNumber > 0 ? s.SetNumber : i + 1,
                            Weight = s.Weight,
                            Reps = s.Reps,
                            IsCompleted = s.IsCompleted
                        }).ToList();
                    }
                    else
                    {
                        activityData.Sets = activity.ActivityData?.Sets;
                    }
                }
                else if (activity.Type == "cardio")
                {
                    activityData.Distance = request.ActivityData.Distance ?? activity.ActivityData?.Distance;
                    activityData.AvgPace = request.ActivityData.AvgPace ?? activity.ActivityData?.AvgPace;
                    activityData.AvgPulse = request.ActivityData.AvgPulse ?? activity.ActivityData?.AvgPulse;
                    activityData.MaxPulse = request.ActivityData.MaxPulse ?? activity.ActivityData?.MaxPulse;
                }

                activity.ActivityData = activityData;
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

            var duration = (request.EndTime ?? request.StartTime?.AddMinutes(30) ?? DateTime.UtcNow.AddMinutes(30))
                          - (request.StartTime ?? DateTime.UtcNow);

            var minutes = (int)duration.TotalMinutes;

            return request.Type?.ToLowerInvariant() == "cardio" ? minutes * 8 : minutes * 5;
        }

        private int CalculateExperienceForActivity(Activity activity)
        {
            int baseExperience = activity.Type == "strength" ? 25 : 20;

            var duration = (activity.EndTime ?? activity.StartTime.AddMinutes(30)) - activity.StartTime;
            int durationBonus = Math.Min(20, (int)duration.TotalMinutes / 10);

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
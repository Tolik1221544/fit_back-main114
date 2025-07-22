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
                        sets.Add(new StrengthSet
                        {
                            SetNumber = 1,
                            Weight = request.StrengthData.WorkingWeight,
                            Reps = 10, 
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
                        Sets = sets,
                        PlankData = request.StrengthData.PlankData != null ? new PlankData
                        {
                            DurationSeconds = request.StrengthData.PlankData.DurationSeconds,
                            PlankType = request.StrengthData.PlankData.PlankType ?? "Классическая планка",
                            Notes = request.StrengthData.PlankData.Notes ?? ""
                        } : null
                    };

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
                        AvgPace = request.CardioData.AvgPace?.Trim() ?? "",
                        JumpRopeData = request.CardioData.JumpRopeData != null ? new JumpRopeData
                        {
                            JumpCount = request.CardioData.JumpRopeData.JumpCount,
                            DurationSeconds = request.CardioData.JumpRopeData.DurationSeconds,
                            RopeType = request.CardioData.JumpRopeData.RopeType ?? "Обычная скакалка",
                            IntervalsCount = request.CardioData.JumpRopeData.IntervalsCount,
                            Notes = request.CardioData.JumpRopeData.Notes ?? ""
                        } : null
                    };
                    activity.StrengthData = null;
                }

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
                    Sets = sets,
                    PlankData = request.StrengthData.PlankData != null ? new PlankData
                    {
                        DurationSeconds = request.StrengthData.PlankData.DurationSeconds,
                        PlankType = request.StrengthData.PlankData.PlankType ?? "Классическая планка",
                        Notes = request.StrengthData.PlankData.Notes ?? ""
                    } : activity.StrengthData?.PlankData
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
                    AvgPace = request.CardioData.AvgPace?.Trim() ?? activity.CardioData?.AvgPace ?? "",
                    JumpRopeData = request.CardioData.JumpRopeData != null ? new JumpRopeData
                    {
                        JumpCount = request.CardioData.JumpRopeData.JumpCount,
                        DurationSeconds = request.CardioData.JumpRopeData.DurationSeconds,
                        RopeType = request.CardioData.JumpRopeData.RopeType ?? "Обычная скакалка",
                        IntervalsCount = request.CardioData.JumpRopeData.IntervalsCount,
                        Notes = request.CardioData.JumpRopeData.Notes ?? ""
                    } : activity.CardioData?.JumpRopeData
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

                var existingSteps = await _stepsRepository.GetByUserIdAndDateAsync(userId, targetDate);

                Steps stepsRecord;

                if (existingSteps != null)
                {
                    existingSteps.StepsCount = request.Steps;
                    existingSteps.Calories = request.Calories.HasValue ? Math.Max(0, request.Calories.Value) : null;
                    stepsRecord = await _stepsRepository.UpdateAsync(existingSteps);

                    _logger.LogInformation($"Updated steps for user {userId} on {targetDate:yyyy-MM-dd}: {request.Steps} steps");
                }
                else
                {
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
                }
                try
                {
                    await _missionService.UpdateMissionProgressAsync(userId, "daily_steps");
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Error updating mission progress: {ex.Message}");
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

                setsBonus = Math.Min(25, setsCount * 3); 
              
                if (totalReps > 50)
                    setsBonus += 10;
                else if (totalReps > 30)
                    setsBonus += 5;
            }

            int plankBonus = 0;
            if (activity.Type?.ToLowerInvariant() == "strength" && activity.StrengthData?.PlankData != null)
            {
                var duration = activity.StrengthData.PlankData.DurationSeconds;
                plankBonus = duration switch
                {
                    >= 120 => 15, 
                    >= 60 => 10,  
                    >= 30 => 5,   
                    _ => 2        
                };
            }

            int jumpRopeBonus = 0;
            if (activity.Type?.ToLowerInvariant() == "cardio" && activity.CardioData?.JumpRopeData != null)
            {
                var jumpCount = activity.CardioData.JumpRopeData.JumpCount;
                jumpRopeBonus = jumpCount switch
                {
                    >= 1000 => 20, 
                    >= 500 => 15,  
                    >= 200 => 10,  
                    >= 100 => 5,   
                    _ => 2        
                };
            }

            return baseExperience + calorieBonus + durationBonus + setsBonus + plankBonus + jumpRopeBonus;
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
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

        public ActivityService(
            IActivityRepository activityRepository,
            IStepsRepository stepsRepository,
            IExperienceService experienceService,
            IMapper mapper)
        {
            _activityRepository = activityRepository;
            _stepsRepository = stepsRepository;
            _experienceService = experienceService;
            _mapper = mapper;
        }

        public async Task<IEnumerable<ActivityDto>> GetUserActivitiesAsync(string userId, string? type = null, DateTime? startDate = null, DateTime? endDate = null)
        {
            var activities = await _activityRepository.GetByUserIdAsync(userId);

            if (!string.IsNullOrEmpty(type))
                activities = activities.Where(a => a.Type.Equals(type, StringComparison.OrdinalIgnoreCase));

            if (startDate.HasValue)
                activities = activities.Where(a => a.StartDate >= startDate.Value);

            if (endDate.HasValue)
                activities = activities.Where(a => a.StartDate <= endDate.Value);

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
            var activity = new Activity
            {
                UserId = userId,
                Type = request.Type,
                StartDate = request.StartDate,
                StartTime = request.StartTime,
                EndDate = request.EndDate,
                EndTime = request.EndTime,
                Calories = request.Calories,
                CreatedAt = DateTime.UtcNow
            };

            if (request.Type == "strength" && request.StrengthData != null)
            {
                activity.StrengthData = new StrengthData
                {
                    Name = request.StrengthData.Name,
                    MuscleGroup = request.StrengthData.MuscleGroup,
                    Equipment = request.StrengthData.Equipment,
                    WorkingWeight = request.StrengthData.WorkingWeight,
                    RestTimeSeconds = request.StrengthData.RestTimeSeconds
                };
            }

            if (request.Type == "cardio" && request.CardioData != null)
            {
                activity.CardioData = new CardioData
                {
                    CardioType = request.CardioData.CardioType,
                    DistanceKm = request.CardioData.DistanceKm,
                    AvgPulse = request.CardioData.AvgPulse,
                    MaxPulse = request.CardioData.MaxPulse,
                    AvgPace = request.CardioData.AvgPace
                };
            }

            var createdActivity = await _activityRepository.CreateAsync(activity);

            // Добавляем опыт за тренировку
            var experienceAmount = CalculateExperienceForActivity(request);
            await _experienceService.AddExperienceAsync(userId, experienceAmount, "activity",
                $"Тренировка: {request.Type} ({experienceAmount} опыта)");

            return _mapper.Map<ActivityDto>(createdActivity);
        }

        public async Task<ActivityDto> UpdateActivityAsync(string userId, string activityId, UpdateActivityRequest request)
        {
            var activity = await _activityRepository.GetByIdAsync(activityId);
            if (activity == null || activity.UserId != userId)
                throw new ArgumentException("Activity not found");

            activity.Type = request.Type;
            activity.StartDate = request.StartDate;
            activity.StartTime = request.StartTime;
            activity.EndDate = request.EndDate;
            activity.EndTime = request.EndTime;
            activity.Calories = request.Calories;

            if (request.Type == "strength" && request.StrengthData != null)
            {
                activity.StrengthData = new StrengthData
                {
                    Name = request.StrengthData.Name,
                    MuscleGroup = request.StrengthData.MuscleGroup,
                    Equipment = request.StrengthData.Equipment,
                    WorkingWeight = request.StrengthData.WorkingWeight,
                    RestTimeSeconds = request.StrengthData.RestTimeSeconds
                };
            }

            if (request.Type == "cardio" && request.CardioData != null)
            {
                activity.CardioData = new CardioData
                {
                    CardioType = request.CardioData.CardioType,
                    DistanceKm = request.CardioData.DistanceKm,
                    AvgPulse = request.CardioData.AvgPulse,
                    MaxPulse = request.CardioData.MaxPulse,
                    AvgPace = request.CardioData.AvgPace
                };
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

            var totalCalories = activities.Where(a => a.Calories.HasValue).Sum(a => a.Calories.Value);

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
            int baseExperience = activity.Type switch
            {
                "strength" => 25, // Силовая тренировка
                "cardio" => 20,   // Кардио тренировка
                _ => 15           // Другие виды
            };

            // Бонус за калории
            int calorieBonus = activity.Calories.HasValue ? (activity.Calories.Value / 100) * 5 : 0;

            // Бонус за продолжительность
            int durationBonus = 0;
            if (activity.EndTime.HasValue && activity.StartTime != default)
            {
                var duration = activity.EndTime.Value - activity.StartTime;
                durationBonus = Math.Min((int)duration.TotalMinutes / 10, 15); // Максимум 15 бонуса
            }

            return baseExperience + calorieBonus + durationBonus;
        }

        private int CalculateExperienceForSteps(int steps)
        {
            return steps switch
            {
                >= 15000 => 30, // Очень активный день
                >= 10000 => 20, // Цель достигнута
                >= 7000 => 15,  // Хорошая активность
                >= 5000 => 10,  // Базовая активность
                _ => 5          // Минимальная активность
            };
        }
    }
}
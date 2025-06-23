using FitnessTracker.API.DTOs;
using FitnessTracker.API.Models;
using FitnessTracker.API.Repositories;
using AutoMapper;

namespace FitnessTracker.API.Services
{
    public class ActivityService : IActivityService
    {
        private readonly IActivityRepository _activityRepository;
        private readonly IMapper _mapper;

        public ActivityService(IActivityRepository activityRepository, IMapper mapper)
        {
            _activityRepository = activityRepository;
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

            return new
            {
                TotalActivities = activities.Count(),
                ActivityTypes = activityTypes,
                MostPopularActivity = activityTypes.OrderByDescending(a => a.Count).FirstOrDefault()?.Type ?? "None",
                LastActivity = activities.OrderByDescending(a => a.CreatedAt).FirstOrDefault()?.CreatedAt
            };
        }
    }
}
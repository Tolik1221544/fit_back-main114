using FitnessTracker.API.DTOs;
using FitnessTracker.API.Models;
using FitnessTracker.API.Repositories;
using AutoMapper;
using System.Text.Json;

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

            // Фильтрация по типу
            if (!string.IsNullOrEmpty(type))
            {
                activities = activities.Where(a => a.Type.Equals(type, StringComparison.OrdinalIgnoreCase));
            }

            // Фильтрация по дате
            if (startDate.HasValue)
            {
                activities = activities.Where(a => a.StartDate >= startDate.Value);
            }

            if (endDate.HasValue)
            {
                activities = activities.Where(a => a.StartDate <= endDate.Value);
            }

            return activities.Select(MapToDto).ToList();
        }

        public async Task<ActivityDto?> GetActivityByIdAsync(string userId, string activityId)
        {
            var activity = await _activityRepository.GetByIdAsync(activityId);
            if (activity == null || activity.UserId != userId)
                return null;

            return MapToDto(activity);
        }

        public async Task<ActivityDto> AddActivityAsync(string userId, AddActivityRequest request)
        {
            var activity = new Activity
            {
                UserId = userId,
                Type = request.Type,
                StartDate = request.StartDate,
                StartTime = request.StartTime
            };

            // Сериализуем данные в JSON
            if (request.Type == "strength" && request.StrengthData != null)
            {
                activity.StrengthDataJson = JsonSerializer.Serialize(request.StrengthData);
            }
            else if (request.Type == "cardio" && request.CardioData != null)
            {
                activity.CardioDataJson = JsonSerializer.Serialize(request.CardioData);
            }

            var createdActivity = await _activityRepository.CreateAsync(activity);
            return MapToDto(createdActivity);
        }

        public async Task<ActivityDto> UpdateActivityAsync(string userId, string activityId, UpdateActivityRequest request)
        {
            var activity = await _activityRepository.GetByIdAsync(activityId);
            if (activity == null || activity.UserId != userId)
                throw new ArgumentException("Activity not found");

            activity.Type = request.Type;
            activity.StartDate = request.StartDate;
            activity.StartTime = request.StartTime;

            // Обновляем данные
            activity.StrengthDataJson = null;
            activity.CardioDataJson = null;

            if (request.Type == "strength" && request.StrengthData != null)
            {
                activity.StrengthDataJson = JsonSerializer.Serialize(request.StrengthData);
            }
            else if (request.Type == "cardio" && request.CardioData != null)
            {
                activity.CardioDataJson = JsonSerializer.Serialize(request.CardioData);
            }

            var updatedActivity = await _activityRepository.UpdateAsync(activity);
            return MapToDto(updatedActivity);
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
                activities = activities.Where(a => a.StartDate >= startDate.Value);

            if (endDate.HasValue)
                activities = activities.Where(a => a.StartDate <= endDate.Value);

            var strengthCount = activities.Count(a => a.Type == "strength");
            var cardioCount = activities.Count(a => a.Type == "cardio");

            // Подсчет общей дистанции для кардио
            var totalDistance = 0m;
            foreach (var activity in activities.Where(a => a.Type == "cardio" && !string.IsNullOrEmpty(a.CardioDataJson)))
            {
                try
                {
                    var cardioData = JsonSerializer.Deserialize<CardioDataDto>(activity.CardioDataJson);
                    if (cardioData != null)
                        totalDistance += cardioData.DistanceKm;
                }
                catch { /* ignore parsing errors */ }
            }

            return new
            {
                TotalActivities = activities.Count(),
                StrengthWorkouts = strengthCount,
                CardioWorkouts = cardioCount,
                TotalDistanceKm = totalDistance,
                WorkoutsThisWeek = activities.Count(a => a.StartDate >= DateTime.UtcNow.AddDays(-7)),
                WorkoutsThisMonth = activities.Count(a => a.StartDate >= DateTime.UtcNow.AddDays(-30))
            };
        }

        private ActivityDto MapToDto(Activity activity)
        {
            var dto = new ActivityDto
            {
                Id = activity.Id,
                Type = activity.Type,
                StartDate = activity.StartDate,
                StartTime = activity.StartTime,
                CreatedAt = activity.CreatedAt
            };

            // Десериализуем данные из JSON
            if (!string.IsNullOrEmpty(activity.StrengthDataJson))
            {
                try
                {
                    dto.StrengthData = JsonSerializer.Deserialize<StrengthDataDto>(activity.StrengthDataJson);
                }
                catch { /* ignore parsing errors */ }
            }

            if (!string.IsNullOrEmpty(activity.CardioDataJson))
            {
                try
                {
                    dto.CardioData = JsonSerializer.Deserialize<CardioDataDto>(activity.CardioDataJson);
                }
                catch { /* ignore parsing errors */ }
            }

            return dto;
        }
    }
}
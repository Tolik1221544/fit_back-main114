using FitnessTracker.API.DTOs;

namespace FitnessTracker.API.Services
{
    public interface IActivityService
    {
        Task<IEnumerable<ActivityDto>> GetUserActivitiesAsync(string userId, string? type = null, DateTime? startDate = null, DateTime? endDate = null);
        Task<ActivityDto?> GetActivityByIdAsync(string userId, string activityId);
        Task<ActivityDto> AddActivityAsync(string userId, AddActivityRequest request);
        Task<ActivityDto> UpdateActivityAsync(string userId, string activityId, UpdateActivityRequest request);
        Task DeleteActivityAsync(string userId, string activityId);
        Task<object> GetActivityStatsAsync(string userId, DateTime? startDate = null, DateTime? endDate = null);

        Task<StepsDto> AddStepsAsync(string userId, AddStepsRequest request);
        Task<IEnumerable<StepsDto>> GetUserStepsAsync(string userId, DateTime? date = null);
    }
}
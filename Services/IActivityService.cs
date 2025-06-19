using FitnessTracker.API.DTOs;

namespace FitnessTracker.API.Services
{
    public interface IActivityService
    {
        Task<IEnumerable<ActivityDto>> GetUserActivitiesAsync(string userId);
        Task<ActivityDto> AddActivityAsync(string userId, AddActivityRequest request);
        Task<ActivityDto> UpdateActivityAsync(string userId, string activityId, UpdateActivityRequest request);
        Task DeleteActivityAsync(string userId, string activityId);
    }
}

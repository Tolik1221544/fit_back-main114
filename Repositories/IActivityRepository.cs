using FitnessTracker.API.Models;

namespace FitnessTracker.API.Repositories
{
    public interface IActivityRepository
    {
        Task<IEnumerable<Activity>> GetByUserIdAsync(string userId);
        Task<Activity?> GetByIdAsync(string id);
        Task<Activity> CreateAsync(Activity activity);
        Task<Activity> UpdateAsync(Activity activity);
        Task DeleteAsync(string id);
        Task<int> GetUserActivityCountAsync(string userId);
    }
}
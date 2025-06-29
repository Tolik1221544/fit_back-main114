using FitnessTracker.API.Models;

namespace FitnessTracker.API.Repositories
{
    public interface IStepsRepository
    {
        Task<IEnumerable<Steps>> GetByUserIdAsync(string userId, DateTime? date = null);

        Task<Steps?> GetByUserIdAndDateAsync(string userId, DateTime date);

        Task<Steps?> GetByIdAsync(string id);
        Task<Steps> CreateAsync(Steps steps);
        Task<Steps> UpdateAsync(Steps steps);
        Task DeleteAsync(string id);
    }
}
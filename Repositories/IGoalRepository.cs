using FitnessTracker.API.Models;

namespace FitnessTracker.API.Repositories
{
    public interface IGoalRepository
    {
        // Goals
        Task<IEnumerable<Goal>> GetUserGoalsAsync(string userId);
        Task<Goal?> GetActiveUserGoalAsync(string userId);
        Task<Goal?> GetGoalByIdAsync(string goalId);
        Task<Goal> CreateGoalAsync(Goal goal);
        Task<Goal> UpdateGoalAsync(Goal goal);
        Task DeleteGoalAsync(string goalId);

        // Daily Progress
        Task<IEnumerable<DailyGoalProgress>> GetDailyProgressAsync(string userId, string goalId, DateTime? startDate = null, DateTime? endDate = null);
        Task<DailyGoalProgress?> GetDailyProgressByDateAsync(string userId, string goalId, DateTime date);
        Task<DailyGoalProgress> CreateDailyProgressAsync(DailyGoalProgress progress);
        Task<DailyGoalProgress> UpdateDailyProgressAsync(DailyGoalProgress progress);
        Task DeleteDailyProgressAsync(string progressId);
    }
}
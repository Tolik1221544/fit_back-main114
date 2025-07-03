using FitnessTracker.API.DTOs;

namespace FitnessTracker.API.Services
{
    public interface IGoalService
    {
        // Goals
        Task<IEnumerable<GoalDto>> GetUserGoalsAsync(string userId);
        Task<GoalDto?> GetActiveUserGoalAsync(string userId);
        Task<GoalDto?> GetGoalByIdAsync(string userId, string goalId);
        Task<GoalDto> CreateGoalAsync(string userId, CreateGoalRequest request);
        Task<GoalDto> UpdateGoalAsync(string userId, string goalId, UpdateGoalRequest request);
        Task DeleteGoalAsync(string userId, string goalId);

        // Daily Progress
        Task<DailyGoalProgressDto?> GetTodayProgressAsync(string userId);
        Task<IEnumerable<DailyGoalProgressDto>> GetProgressHistoryAsync(string userId, string goalId, DateTime? startDate = null, DateTime? endDate = null);
        Task<DailyGoalProgressDto> UpdateDailyProgressAsync(string userId, UpdateDailyProgressRequest request);
        Task RecalculateDailyProgressAsync(string userId, DateTime date);

        // Templates
        Task<IEnumerable<GoalTemplateDto>> GetGoalTemplatesAsync();
        Task<GoalTemplateDto?> GetGoalTemplateAsync(string goalType);

        // Auto-calculation
        Task UpdateAllUserProgressAsync(string userId, DateTime date);
    }
}
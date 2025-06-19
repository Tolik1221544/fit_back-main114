namespace FitnessTracker.API.Services
{
    public interface IStatsService
    {
        Task<object> GetUserStatsAsync(string userId);
        Task<object> GetNutritionStatsAsync(string userId, DateTime? startDate = null, DateTime? endDate = null);
        Task<object> GetActivityStatsAsync(string userId, DateTime? startDate = null, DateTime? endDate = null);
    }
}

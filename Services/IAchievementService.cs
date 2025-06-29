using FitnessTracker.API.DTOs;

namespace FitnessTracker.API.Services
{
    public interface IAchievementService
    {
        Task<IEnumerable<AchievementDto>> GetUserAchievementsAsync(string userId);
        Task CheckAndUnlockAchievementsAsync(string userId);
        Task<bool> UnlockAchievementAsync(string userId, string achievementId, int currentProgress);
    }
}
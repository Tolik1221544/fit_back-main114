using FitnessTracker.API.DTOs;

namespace FitnessTracker.API.Services
{
    public interface IMissionService
    {
        Task<IEnumerable<MissionDto>> GetUserMissionsAsync(string userId);
        Task<IEnumerable<AchievementDto>> GetUserAchievementsAsync(string userId);
        Task UpdateMissionProgressAsync(string userId, string missionType, int incrementValue = 1);
    }
}

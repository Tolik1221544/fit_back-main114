using FitnessTracker.API.Models;

namespace FitnessTracker.API.Repositories
{
    public interface IMissionRepository
    {
        Task<IEnumerable<Mission>> GetActiveMissionsAsync();
        Task<IEnumerable<UserMission>> GetUserMissionsAsync(string userId);
        Task<UserMission?> GetUserMissionAsync(string userId, string missionId);
        Task<UserMission> CreateUserMissionAsync(UserMission userMission);
        Task<UserMission> UpdateUserMissionAsync(UserMission userMission);
        Task<IEnumerable<Achievement>> GetUserAchievementsAsync(string userId);
    }
}

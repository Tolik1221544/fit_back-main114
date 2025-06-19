using FitnessTracker.API.Data;
using FitnessTracker.API.Models;
using Microsoft.EntityFrameworkCore;

namespace FitnessTracker.API.Repositories
{
    public class MissionRepository : IMissionRepository
    {
        private readonly ApplicationDbContext _context;

        public MissionRepository(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IEnumerable<Mission>> GetActiveMissionsAsync()
        {
            return await _context.Missions
                .Where(m => m.IsActive)
                .ToListAsync();
        }

        public async Task<IEnumerable<UserMission>> GetUserMissionsAsync(string userId)
        {
            return await _context.UserMissions
                .Include(um => um.Mission)
                .Where(um => um.UserId == userId)
                .ToListAsync();
        }

        public async Task<UserMission?> GetUserMissionAsync(string userId, string missionId)
        {
            return await _context.UserMissions
                .FirstOrDefaultAsync(um => um.UserId == userId && um.MissionId == missionId);
        }

        public async Task<UserMission> CreateUserMissionAsync(UserMission userMission)
        {
            _context.UserMissions.Add(userMission);
            await _context.SaveChangesAsync();
            return userMission;
        }

        public async Task<UserMission> UpdateUserMissionAsync(UserMission userMission)
        {
            _context.UserMissions.Update(userMission);
            await _context.SaveChangesAsync();
            return userMission;
        }

        public async Task<IEnumerable<Achievement>> GetUserAchievementsAsync(string userId)
        {
            return await _context.Achievements
                .OrderByDescending(a => a.UnlockedAt)
                .ToListAsync();
        }
    }
}

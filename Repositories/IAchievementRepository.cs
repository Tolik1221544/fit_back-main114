using FitnessTracker.API.Data;
using FitnessTracker.API.Models;
using Microsoft.EntityFrameworkCore;

namespace FitnessTracker.API.Repositories
{
    public class AchievementRepository : IAchievementRepository
    {
        private readonly ApplicationDbContext _context;

        public AchievementRepository(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IEnumerable<Achievement>> GetAllAchievementsAsync()
        {
            return await _context.Achievements.Where(a => a.IsActive).ToListAsync();
        }

        public async Task<Achievement?> GetAchievementByIdAsync(string id)
        {
            return await _context.Achievements.FirstOrDefaultAsync(a => a.Id == id);
        }

        public async Task<IEnumerable<UserAchievement>> GetUserAchievementsAsync(string userId)
        {
            return await _context.UserAchievements
                .Include(ua => ua.Achievement)
                .Where(ua => ua.UserId == userId)
                .OrderByDescending(ua => ua.UnlockedAt)
                .ToListAsync();
        }

        public async Task<UserAchievement?> GetUserAchievementAsync(string userId, string achievementId)
        {
            return await _context.UserAchievements
                .FirstOrDefaultAsync(ua => ua.UserId == userId && ua.AchievementId == achievementId);
        }

        public async Task<UserAchievement> CreateUserAchievementAsync(UserAchievement userAchievement)
        {
            _context.UserAchievements.Add(userAchievement);
            await _context.SaveChangesAsync();
            return userAchievement;
        }
    }
}
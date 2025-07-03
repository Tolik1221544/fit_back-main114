using FitnessTracker.API.Data;
using FitnessTracker.API.Models;
using Microsoft.EntityFrameworkCore;

namespace FitnessTracker.API.Repositories
{
    public class GoalRepository : IGoalRepository
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<GoalRepository> _logger;

        public GoalRepository(ApplicationDbContext context, ILogger<GoalRepository> logger)
        {
            _context = context;
            _logger = logger;
        }

        // Goals
        public async Task<IEnumerable<Goal>> GetUserGoalsAsync(string userId)
        {
            return await _context.Goals
                .Where(g => g.UserId == userId)
                .Include(g => g.DailyProgress)
                .OrderByDescending(g => g.CreatedAt)
                .ToListAsync();
        }

        public async Task<Goal?> GetActiveUserGoalAsync(string userId)
        {
            return await _context.Goals
                .Where(g => g.UserId == userId && g.IsActive)
                .Include(g => g.DailyProgress)
                .FirstOrDefaultAsync();
        }

        public async Task<Goal?> GetGoalByIdAsync(string goalId)
        {
            return await _context.Goals
                .Include(g => g.DailyProgress)
                .FirstOrDefaultAsync(g => g.Id == goalId);
        }

        public async Task<Goal> CreateGoalAsync(Goal goal)
        {
            _context.Goals.Add(goal);
            await _context.SaveChangesAsync();
            return goal;
        }

        public async Task<Goal> UpdateGoalAsync(Goal goal)
        {
            _context.Goals.Update(goal);
            await _context.SaveChangesAsync();
            return goal;
        }

        public async Task DeleteGoalAsync(string goalId)
        {
            var goal = await GetGoalByIdAsync(goalId);
            if (goal != null)
            {
                _context.Goals.Remove(goal);
                await _context.SaveChangesAsync();
            }
        }

        // Daily Progress
        public async Task<IEnumerable<DailyGoalProgress>> GetDailyProgressAsync(string userId, string goalId, DateTime? startDate = null, DateTime? endDate = null)
        {
            var query = _context.DailyGoalProgress
                .Where(dp => dp.UserId == userId && dp.GoalId == goalId);

            if (startDate.HasValue)
                query = query.Where(dp => dp.Date >= startDate.Value.Date);

            if (endDate.HasValue)
                query = query.Where(dp => dp.Date <= endDate.Value.Date);

            return await query
                .OrderByDescending(dp => dp.Date)
                .ToListAsync();
        }

        public async Task<DailyGoalProgress?> GetDailyProgressByDateAsync(string userId, string goalId, DateTime date)
        {
            return await _context.DailyGoalProgress
                .FirstOrDefaultAsync(dp => dp.UserId == userId &&
                                         dp.GoalId == goalId &&
                                         dp.Date.Date == date.Date);
        }

        public async Task<DailyGoalProgress> CreateDailyProgressAsync(DailyGoalProgress progress)
        {
            _context.DailyGoalProgress.Add(progress);
            await _context.SaveChangesAsync();
            return progress;
        }

        public async Task<DailyGoalProgress> UpdateDailyProgressAsync(DailyGoalProgress progress)
        {
            progress.UpdatedAt = DateTime.UtcNow;
            _context.DailyGoalProgress.Update(progress);
            await _context.SaveChangesAsync();
            return progress;
        }

        public async Task DeleteDailyProgressAsync(string progressId)
        {
            var progress = await _context.DailyGoalProgress.FindAsync(progressId);
            if (progress != null)
            {
                _context.DailyGoalProgress.Remove(progress);
                await _context.SaveChangesAsync();
            }
        }
    }
}
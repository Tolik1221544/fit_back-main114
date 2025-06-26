using FitnessTracker.API.Data;
using FitnessTracker.API.Models;
using Microsoft.EntityFrameworkCore;

namespace FitnessTracker.API.Repositories
{
    // IBodyScanRepository
    public interface IBodyScanRepository
    {
        Task<IEnumerable<BodyScan>> GetByUserIdAsync(string userId);
        Task<BodyScan?> GetByIdAsync(string id);
        Task<BodyScan> CreateAsync(BodyScan bodyScan);
        Task<BodyScan> UpdateAsync(BodyScan bodyScan);
        Task DeleteAsync(string id);
    }

    public class BodyScanRepository : IBodyScanRepository
    {
        private readonly ApplicationDbContext _context;

        public BodyScanRepository(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IEnumerable<BodyScan>> GetByUserIdAsync(string userId)
        {
            return await _context.BodyScans
                .Where(bs => bs.UserId == userId)
                .OrderByDescending(bs => bs.ScanDate)
                .ToListAsync();
        }

        public async Task<BodyScan?> GetByIdAsync(string id)
        {
            return await _context.BodyScans.FirstOrDefaultAsync(bs => bs.Id == id);
        }

        public async Task<BodyScan> CreateAsync(BodyScan bodyScan)
        {
            _context.BodyScans.Add(bodyScan);
            await _context.SaveChangesAsync();
            return bodyScan;
        }

        public async Task<BodyScan> UpdateAsync(BodyScan bodyScan)
        {
            _context.BodyScans.Update(bodyScan);
            await _context.SaveChangesAsync();
            return bodyScan;
        }

        public async Task DeleteAsync(string id)
        {
            var bodyScan = await GetByIdAsync(id);
            if (bodyScan != null)
            {
                _context.BodyScans.Remove(bodyScan);
                await _context.SaveChangesAsync();
            }
        }
    }

    // IExperienceRepository
    public interface IExperienceRepository
    {
        Task<IEnumerable<ExperienceTransaction>> GetUserTransactionsAsync(string userId);
        Task<ExperienceTransaction> CreateTransactionAsync(ExperienceTransaction transaction);
    }

    public class ExperienceRepository : IExperienceRepository
    {
        private readonly ApplicationDbContext _context;

        public ExperienceRepository(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IEnumerable<ExperienceTransaction>> GetUserTransactionsAsync(string userId)
        {
            return await _context.ExperienceTransactions
                .Where(et => et.UserId == userId)
                .OrderByDescending(et => et.CreatedAt)
                .ToListAsync();
        }

        public async Task<ExperienceTransaction> CreateTransactionAsync(ExperienceTransaction transaction)
        {
            _context.ExperienceTransactions.Add(transaction);
            await _context.SaveChangesAsync();
            return transaction;
        }
    }

    public interface IAchievementRepository
    {
        Task<IEnumerable<Achievement>> GetAllAchievementsAsync();
        Task<Achievement?> GetAchievementByIdAsync(string id);
        Task<IEnumerable<UserAchievement>> GetUserAchievementsAsync(string userId);
        Task<UserAchievement?> GetUserAchievementAsync(string userId, string achievementId);
        Task<UserAchievement> CreateUserAchievementAsync(UserAchievement userAchievement);
    }
}
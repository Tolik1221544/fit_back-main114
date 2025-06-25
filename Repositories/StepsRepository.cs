using FitnessTracker.API.Data;
using FitnessTracker.API.Models;
using Microsoft.EntityFrameworkCore;

namespace FitnessTracker.API.Repositories
{
    public class StepsRepository : IStepsRepository
    {
        private readonly ApplicationDbContext _context;

        public StepsRepository(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IEnumerable<Steps>> GetByUserIdAsync(string userId, DateTime? date = null)
        {
            var query = _context.Steps.Where(s => s.UserId == userId);

            if (date.HasValue)
            {
                var targetDate = date.Value.Date;
                query = query.Where(s => s.Date.Date == targetDate);
            }

            return await query.OrderByDescending(s => s.Date).ToListAsync();
        }

        public async Task<Steps?> GetByIdAsync(string id)
        {
            return await _context.Steps.FirstOrDefaultAsync(s => s.Id == id);
        }

        public async Task<Steps> CreateAsync(Steps steps)
        {
            _context.Steps.Add(steps);
            await _context.SaveChangesAsync();
            return steps;
        }

        public async Task<Steps> UpdateAsync(Steps steps)
        {
            _context.Steps.Update(steps);
            await _context.SaveChangesAsync();
            return steps;
        }

        public async Task DeleteAsync(string id)
        {
            var steps = await GetByIdAsync(id);
            if (steps != null)
            {
                _context.Steps.Remove(steps);
                await _context.SaveChangesAsync();
            }
        }
    }
}
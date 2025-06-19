using FitnessTracker.API.Data;
using FitnessTracker.API.Models;
using Microsoft.EntityFrameworkCore;

namespace FitnessTracker.API.Repositories
{
    public class FoodIntakeRepository : IFoodIntakeRepository
    {
        private readonly ApplicationDbContext _context;

        public FoodIntakeRepository(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IEnumerable<FoodIntake>> GetByUserIdAsync(string userId)
        {
            return await _context.FoodIntakes
                .Where(f => f.UserId == userId)
                .OrderByDescending(f => f.DateTime)
                .ToListAsync();
        }

        public async Task<IEnumerable<FoodIntake>> GetByUserIdAndDateAsync(string userId, DateTime date)
        {
            var startDate = date.Date;
            var endDate = startDate.AddDays(1);
            
            return await _context.FoodIntakes
                .Where(f => f.UserId == userId && f.DateTime >= startDate && f.DateTime < endDate)
                .OrderBy(f => f.DateTime)
                .ToListAsync();
        }

        public async Task<FoodIntake?> GetByIdAsync(string id)
        {
            return await _context.FoodIntakes.FirstOrDefaultAsync(f => f.Id == id);
        }

        public async Task<FoodIntake> CreateAsync(FoodIntake foodIntake)
        {
            _context.FoodIntakes.Add(foodIntake);
            await _context.SaveChangesAsync();
            return foodIntake;
        }

        public async Task<FoodIntake> UpdateAsync(FoodIntake foodIntake)
        {
            _context.FoodIntakes.Update(foodIntake);
            await _context.SaveChangesAsync();
            return foodIntake;
        }

        public async Task DeleteAsync(string id)
        {
            var foodIntake = await GetByIdAsync(id);
            if (foodIntake != null)
            {
                _context.FoodIntakes.Remove(foodIntake);
                await _context.SaveChangesAsync();
            }
        }

        public async Task<IEnumerable<FoodIntake>> CreateManyAsync(IEnumerable<FoodIntake> foodIntakes)
        {
            _context.FoodIntakes.AddRange(foodIntakes);
            await _context.SaveChangesAsync();
            return foodIntakes;
        }
    }
}

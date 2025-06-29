using FitnessTracker.API.Data;
using FitnessTracker.API.Models;
using Microsoft.EntityFrameworkCore;

namespace FitnessTracker.API.Repositories
{
    public class StepsRepository : IStepsRepository
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<StepsRepository> _logger;

        public StepsRepository(ApplicationDbContext context, ILogger<StepsRepository> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<IEnumerable<Steps>> GetByUserIdAsync(string userId, DateTime? date = null)
        {
            try
            {
                var query = _context.Steps.Where(s => s.UserId == userId);

                if (date.HasValue)
                {
                    var targetDate = date.Value.Date;
                    query = query.Where(s => s.Date.Date == targetDate);
                }

                return await query.OrderByDescending(s => s.Date).ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error getting steps for user {userId}: {ex.Message}");
                throw;
            }
        }

        public async Task<Steps?> GetByUserIdAndDateAsync(string userId, DateTime date)
        {
            try
            {
                var targetDate = date.Date;
                return await _context.Steps
                    .FirstOrDefaultAsync(s => s.UserId == userId && s.Date.Date == targetDate);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error getting steps for user {userId} on date {date:yyyy-MM-dd}: {ex.Message}");
                throw;
            }
        }

        public async Task<Steps?> GetByIdAsync(string id)
        {
            try
            {
                return await _context.Steps.FirstOrDefaultAsync(s => s.Id == id);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error getting steps by ID {id}: {ex.Message}");
                throw;
            }
        }

        public async Task<Steps> CreateAsync(Steps steps)
        {
            try
            {
                // Проверяем, что пользователь существует
                var userExists = await _context.Users.AnyAsync(u => u.Id == steps.UserId);
                if (!userExists)
                {
                    throw new InvalidOperationException($"User {steps.UserId} does not exist");
                }

                // Проверяем дубликаты по дате
                var existingSteps = await GetByUserIdAndDateAsync(steps.UserId, steps.Date);
                if (existingSteps != null)
                {
                    throw new InvalidOperationException($"Steps for user {steps.UserId} on date {steps.Date:yyyy-MM-dd} already exist. Use Update instead.");
                }

                _context.Steps.Add(steps);
                await _context.SaveChangesAsync();

                _logger.LogInformation($"Created steps record for user {steps.UserId}: {steps.StepsCount} steps on {steps.Date:yyyy-MM-dd}");
                return steps;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error creating steps for user {steps.UserId}: {ex.Message}");
                throw;
            }
        }

        public async Task<Steps> UpdateAsync(Steps steps)
        {
            try
            {
                // Проверяем, что запись существует
                var existingSteps = await GetByIdAsync(steps.Id);
                if (existingSteps == null)
                {
                    throw new InvalidOperationException($"Steps record {steps.Id} not found");
                }

                // Обновляем поля
                existingSteps.StepsCount = steps.StepsCount;
                existingSteps.Calories = steps.Calories;
                existingSteps.Date = steps.Date;

                await _context.SaveChangesAsync();

                _logger.LogInformation($"Updated steps record for user {steps.UserId}: {steps.StepsCount} steps on {steps.Date:yyyy-MM-dd}");
                return existingSteps;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error updating steps {steps.Id}: {ex.Message}");
                throw;
            }
        }

        public async Task DeleteAsync(string id)
        {
            try
            {
                var steps = await GetByIdAsync(id);
                if (steps != null)
                {
                    _context.Steps.Remove(steps);
                    await _context.SaveChangesAsync();
                    _logger.LogInformation($"Deleted steps record {id}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error deleting steps {id}: {ex.Message}");
                throw;
            }
        }
    }
}
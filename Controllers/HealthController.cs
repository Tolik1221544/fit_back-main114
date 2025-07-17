using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using FitnessTracker.API.Data;

namespace FitnessTracker.API.Controllers
{
    [ApiController]
    [Route("api/health")]
    public class HealthController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<HealthController> _logger;

        public HealthController(ApplicationDbContext context, ILogger<HealthController> logger)
        {
            _context = context;
            _logger = logger;
        }

        [HttpGet]
        public async Task<IActionResult> CheckHealth()
        {
            try
            {
                var canConnect = await _context.Database.CanConnectAsync();

                var userCount = await _context.Users.CountAsync();

                var lastActivity = await _context.Activities
                    .OrderByDescending(a => a.CreatedAt)
                    .Select(a => a.CreatedAt)
                    .FirstOrDefaultAsync();

                return Ok(new
                {
                    status = "healthy",
                    database = new
                    {
                        canConnect = canConnect,
                        userCount = userCount,
                        lastActivity = lastActivity
                    },
                    timestamp = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                _logger.LogError($"Health check failed: {ex.Message}");
                return StatusCode(503, new
                {
                    status = "unhealthy",
                    error = ex.Message,
                    timestamp = DateTime.UtcNow
                });
            }
        }

        [HttpGet("db-info")]
        public async Task<IActionResult> GetDatabaseInfo()
        {
            try
            {
                var userCount = await _context.Users.CountAsync();
                var activityCount = await _context.Activities.CountAsync();
                var stepsCount = await _context.Steps.CountAsync();
                var foodIntakeCount = await _context.FoodIntakes.CountAsync();

                return Ok(new
                {
                    users = userCount,
                    activities = activityCount,
                    stepsRecords = stepsCount,
                    foodIntakes = foodIntakeCount,
                    timestamp = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                _logger.LogError($"Database info failed: {ex.Message}");
                return BadRequest(new { error = ex.Message });
            }
        }
    }
}
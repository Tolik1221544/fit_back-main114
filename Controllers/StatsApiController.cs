using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using FitnessTracker.API.Data;
using Microsoft.EntityFrameworkCore;
using FitnessTracker.API.Models;

namespace FitnessTracker.API.Controllers
{
    [ApiController]
    [Route("api/stats")]
    [Authorize]
    public class StatsApiController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<StatsApiController> _logger;

        public StatsApiController(ApplicationDbContext context, ILogger<StatsApiController> logger)
        {
            _context = context;
            _logger = logger;
        }

        [HttpGet("coin-spending-daily")]
        public async Task<IActionResult> GetCoinSpendingDaily([FromQuery] int days = 30)
        {
            try
            {
                var startDate = DateTime.UtcNow.AddDays(-days).Date;

                var spendingData = await _context.LwCoinTransactions
                    .Where(t => t.Type == "spent" &&
                                t.CreatedAt >= startDate &&
                                t.Amount < 0)
                    .GroupBy(t => t.CreatedAt.Date)
                    .Select(g => new
                    {
                        Date = g.Key.ToString("yyyy-MM-dd"),
                        TotalSpent = g.Sum(t => Math.Abs(t.FractionalAmount > 0 ? (decimal)t.FractionalAmount : t.Amount))
                    })
                    .OrderBy(x => x.Date)
                    .ToListAsync();

                return Ok(new { success = true, data = spendingData });
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error getting spending stats: {ex.Message}");
                return StatusCode(500, new { error = "Internal server error" });
            }
        }

        [HttpGet("revenue-daily")]
        public async Task<IActionResult> GetRevenueDaily([FromQuery] int days = 30)
        {
            try
            {
                var startDate = DateTime.UtcNow.AddDays(-days).Date;

                var revenueData = await _context.PendingPayments
                    .Where(p => p.Status == "completed" &&
                                p.CompletedAt.HasValue &&
                                p.CompletedAt.Value >= startDate) 
                    .GroupBy(p => p.CompletedAt!.Value.Date) 
                    .Select(g => new
                    {
                        Date = g.Key.ToString("yyyy-MM-dd"),
                        TotalRevenue = g.Sum(p => p.Amount)
                    })
                    .OrderBy(x => x.Date)
                    .ToListAsync();

                return Ok(new { success = true, data = revenueData });
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error getting revenue stats: {ex.Message}");
                return StatusCode(500, new { error = "Internal server error" });
            }
        }
    }
}
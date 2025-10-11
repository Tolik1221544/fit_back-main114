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
                _logger.LogInformation($"📊 Getting coin spending for {days} days");

                var startDate = DateTime.UtcNow.AddDays(-days).Date;

                var transactions = await _context.LwCoinTransactions
                    .Where(t => t.Type == "spent" &&
                                t.CreatedAt >= startDate &&
                                t.Amount < 0)
                    .Select(t => new
                    {
                        Date = t.CreatedAt,
                        Amount = t.FractionalAmount > 0 ? (decimal)t.FractionalAmount : t.Amount
                    })
                    .ToListAsync();

                _logger.LogInformation($"📊 Found {transactions.Count} spending transactions");

                var spendingData = transactions
                    .GroupBy(t => t.Date.Date)
                    .Select(g => new
                    {
                        Date = g.Key.ToString("yyyy-MM-dd"),
                        TotalSpent = g.Sum(t => Math.Abs(t.Amount))
                    })
                    .OrderBy(x => x.Date)
                    .ToList();

                _logger.LogInformation($"✅ Grouped into {spendingData.Count} days");

                return Ok(new { success = true, data = spendingData });
            }
            catch (Exception ex)
            {
                _logger.LogError($"❌ Error getting spending stats: {ex.Message}");
                _logger.LogError($"Stack trace: {ex.StackTrace}");
                return StatusCode(500, new { error = "Internal server error", details = ex.Message });
            }
        }

        [HttpGet("revenue-daily")]
        public async Task<IActionResult> GetRevenueDaily([FromQuery] int days = 30)
        {
            try
            {
                _logger.LogInformation($"📈 Getting ALL revenue sources for {days} days");

                var startDate = DateTime.UtcNow.AddDays(-days).Date;

                var tributePayments = await _context.PendingPayments
                    .Where(p => p.Status == "completed" &&
                                p.CompletedAt.HasValue &&
                                p.CompletedAt.Value >= startDate)
                    .Select(p => new
                    {
                        Date = p.CompletedAt!.Value,
                        Amount = p.Amount,
                        Source = "Tribute"
                    })
                    .ToListAsync();

                _logger.LogInformation($"📱 Found {tributePayments.Count} Tribute payments");

                var mobileSubscriptions = await _context.Subscriptions
                    .Where(s => s.PurchasedAt >= startDate)
                    .Select(s => new
                    {
                        Date = s.PurchasedAt,
                        Amount = s.Price,
                        Source = s.Type.Contains("premium") ? "Premium" : "Mobile"
                    })
                    .ToListAsync();

                _logger.LogInformation($"📱 Found {mobileSubscriptions.Count} mobile subscriptions");

                var allPayments = tributePayments
                    .Select(p => new { p.Date, p.Amount })
                    .Concat(mobileSubscriptions.Select(s => new { s.Date, s.Amount }))
                    .ToList();

                _logger.LogInformation($"💰 Total payment records: {allPayments.Count}");

                var revenueData = allPayments
                    .GroupBy(p => p.Date.Date)
                    .Select(g => new
                    {
                        Date = g.Key.ToString("yyyy-MM-dd"),
                        TotalRevenue = g.Sum(p => p.Amount)
                    })
                    .OrderBy(x => x.Date)
                    .ToList();

                _logger.LogInformation($"✅ Grouped into {revenueData.Count} days");
                _logger.LogInformation($"💵 Total revenue: {revenueData.Sum(r => r.TotalRevenue):F2} €");

                return Ok(new { success = true, data = revenueData });
            }
            catch (Exception ex)
            {
                _logger.LogError($"❌ Error getting revenue stats: {ex.Message}");
                _logger.LogError($"Stack trace: {ex.StackTrace}");
                return StatusCode(500, new { error = "Internal server error", details = ex.Message });
            }
        }

        [HttpGet("revenue-by-source")]
        public async Task<IActionResult> GetRevenueBySource([FromQuery] int days = 30)
        {
            try
            {
                _logger.LogInformation($"📊 Getting revenue by source for {days} days");

                var startDate = DateTime.UtcNow.AddDays(-days).Date;

                var tributeRevenue = await _context.PendingPayments
                    .Where(p => p.Status == "completed" &&
                                p.CompletedAt.HasValue &&
                                p.CompletedAt.Value >= startDate)
                    .SumAsync(p => p.Amount);

                var mobileRevenue = await _context.Subscriptions
                    .Where(s => s.PurchasedAt >= startDate)
                    .SumAsync(s => s.Price);

                var response = new
                {
                    success = true,
                    data = new
                    {
                        tribute = new
                        {
                            revenue = tributeRevenue,
                            count = await _context.PendingPayments
                                .CountAsync(p => p.Status == "completed" &&
                                                p.CompletedAt.HasValue &&
                                                p.CompletedAt.Value >= startDate)
                        },
                        mobile = new
                        {
                            revenue = mobileRevenue,
                            count = await _context.Subscriptions
                                .CountAsync(s => s.PurchasedAt >= startDate)
                        },
                        total = new
                        {
                            revenue = tributeRevenue + mobileRevenue,
                            count = await _context.PendingPayments
                                .CountAsync(p => p.Status == "completed" &&
                                                p.CompletedAt.HasValue &&
                                                p.CompletedAt.Value >= startDate) +
                                   await _context.Subscriptions
                                .CountAsync(s => s.PurchasedAt >= startDate)
                        }
                    }
                };

                _logger.LogInformation($"✅ Revenue breakdown: Tribute={tributeRevenue:F2}€, Mobile={mobileRevenue:F2}€");

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError($"❌ Error getting revenue by source: {ex.Message}");
                return StatusCode(500, new { error = "Internal server error", details = ex.Message });
            }
        }
    }
}
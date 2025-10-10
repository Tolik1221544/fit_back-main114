using Microsoft.AspNetCore.Mvc;
using FitnessTracker.API.Services;
using FitnessTracker.API.Repositories;
using FitnessTracker.API.Data;
using FitnessTracker.API.Models; 

namespace FitnessTracker.API.Controllers
{
    [ApiController]
    [Route("api/tribute-pending")]
    public class TributePendingController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly IUserRepository _userRepository;
        private readonly ILogger<TributePendingController> _logger;

        public TributePendingController(
            ApplicationDbContext context,
            IUserRepository userRepository,
            ILogger<TributePendingController> logger)
        {
            _context = context;
            _userRepository = userRepository;
            _logger = logger;
        }

        [HttpPost("create")]
        public async Task<IActionResult> CreatePendingPayment([FromBody] CreatePendingPaymentRequest request)
        {
            try
            {
                _logger.LogInformation($"📝 Creating pending payment for Telegram ID: {request.TelegramId}");

                var user = await _userRepository.GetByTelegramIdAsync(request.TelegramId);
                if (user == null)
                {
                    _logger.LogWarning($"⚠️ User not found for Telegram ID: {request.TelegramId}");
                    return BadRequest(new { error = "User not found" });
                }

                var pendingPayment = new PendingPayment 
                {
                    PaymentId = request.OrderId,
                    TelegramId = request.TelegramId,
                    UserId = user.Id,
                    Amount = request.Amount,
                    Currency = request.Currency,
                    PackageId = request.PackageId,
                    CoinsAmount = request.CoinsAmount,
                    DurationDays = request.DurationDays,
                    Status = "pending"
                };

                _context.PendingPayments.Add(pendingPayment);
                await _context.SaveChangesAsync();

                _logger.LogInformation($"✅ Pending payment created: {request.OrderId}");

                return Ok(new
                {
                    success = true,
                    pendingPaymentId = pendingPayment.Id,
                    message = "Pending payment created successfully"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError($"❌ Error creating pending payment: {ex.Message}");
                return StatusCode(500, new { error = "Internal server error" });
            }
        }
    }

    public class CreatePendingPaymentRequest
    {
        public string OrderId { get; set; } = "";
        public long TelegramId { get; set; }
        public decimal Amount { get; set; }
        public string Currency { get; set; } = "EUR";
        public string PackageId { get; set; } = "";
        public int CoinsAmount { get; set; }
        public int DurationDays { get; set; }
    }
}
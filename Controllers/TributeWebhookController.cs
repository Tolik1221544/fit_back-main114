using Microsoft.AspNetCore.Mvc;
using FitnessTracker.API.Services;
using FitnessTracker.API.Repositories;
using FitnessTracker.API.Data;
using FitnessTracker.API.Models;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace FitnessTracker.API.Controllers
{
    [ApiController]
    [Route("api/tribute")]
    public class TributeWebhookController : ControllerBase
    {
        private readonly ILwCoinService _lwCoinService;
        private readonly IUserRepository _userRepository;
        private readonly ApplicationDbContext _context;
        private readonly IConfiguration _configuration;
        private readonly ILogger<TributeWebhookController> _logger;

        private static readonly HashSet<string> _processedPayments = new();
        private static readonly object _lock = new();

        public TributeWebhookController(
            ILwCoinService lwCoinService,
            IUserRepository userRepository,
            ApplicationDbContext context,
            IConfiguration configuration,
            ILogger<TributeWebhookController> logger)
        {
            _lwCoinService = lwCoinService;
            _userRepository = userRepository;
            _context = context;
            _configuration = configuration;
            _logger = logger;
        }

        [HttpPost("webhook")]
        public async Task<IActionResult> HandleWebhook()
        {
            try
            {
                using var reader = new StreamReader(Request.Body);
                var body = await reader.ReadAsStringAsync();

                _logger.LogInformation($"📥 Tribute webhook received");
                _logger.LogInformation($"Body: {body}");

                var signature = Request.Headers["trbt-signature"].FirstOrDefault();

                if (string.IsNullOrEmpty(signature))
                {
                    _logger.LogWarning("⚠️ Webhook without signature");
                    return BadRequest(new { error = "Missing signature" });
                }

                var webhookSecret = _configuration["Tribute:WebhookSecret"];
                if (!VerifySignature(body, signature, webhookSecret))
                {
                    _logger.LogWarning("❌ Invalid webhook signature");
                    return Unauthorized(new { error = "Invalid signature" });
                }

                var webhookData = JsonSerializer.Deserialize<TributeWebhookDto>(body, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (webhookData == null || webhookData.Payload == null)
                {
                    _logger.LogWarning("⚠️ Invalid webhook payload");
                    return BadRequest(new { error = "Invalid payload" });
                }

                _logger.LogInformation($"💳 Webhook event: {webhookData.Name}");
                _logger.LogInformation($"   Product ID: {webhookData.Payload.ProductId}");
                _logger.LogInformation($"   Telegram User ID: {webhookData.Payload.TelegramUserId}");
                _logger.LogInformation($"   User ID: {webhookData.Payload.UserId}");
                _logger.LogInformation($"   Amount: {webhookData.Payload.Amount} {webhookData.Payload.Currency}");

                if (webhookData.Name == "new_digital_product")
                {
                    return await ProcessSuccessfulPurchaseAsync(webhookData);
                }

                return Ok(new { status = "ok", message = "Event ignored" });
            }
            catch (Exception ex)
            {
                _logger.LogError($"❌ Webhook error: {ex.Message}");
                _logger.LogError($"Stack trace: {ex.StackTrace}");
                return StatusCode(500, new { error = "Internal error" });
            }
        }

        private async Task<IActionResult> ProcessSuccessfulPurchaseAsync(TributeWebhookDto webhookData)
        {
            var payload = webhookData.Payload!;

            var telegramUserId = payload.TelegramUserId;

            if (telegramUserId == null)
            {
                _logger.LogWarning($"⚠️ No telegram_user_id in webhook payload");
                return BadRequest(new { error = "Missing telegram_user_id" });
            }

            var paymentKey = $"{telegramUserId}_{payload.ProductId}_{payload.Amount}_{webhookData.CreatedAt:yyyyMMddHHmmss}";
            lock (_lock)
            {
                if (_processedPayments.Contains(paymentKey))
                {
                    _logger.LogInformation($"⚠️ Payment already processed: {paymentKey}");
                    return Ok(new { status = "already_processed" });
                }
                _processedPayments.Add(paymentKey);
            }

            try
            {
                var user = await _userRepository.GetByTelegramIdAsync(telegramUserId.Value);
                if (user == null)
                {
                    _logger.LogWarning($"⚠️ User not found for Telegram ID {telegramUserId}");
                    lock (_lock) { _processedPayments.Remove(paymentKey); }
                    return NotFound(new { error = "User not found" });
                }

                var (coins, days, packageName) = DeterminePackageByAmount(payload.Amount ?? 0);

                if (coins == 0)
                {
                    _logger.LogWarning($"⚠️ Unknown amount: {payload.Amount}");
                    lock (_lock) { _processedPayments.Remove(paymentKey); }
                    return BadRequest(new { error = "Unknown package amount" });
                }

                var success = await _lwCoinService.PurchaseSubscriptionCoinsAsync(
                    user.Id,
                    coins,
                    days,
                    payload.Amount ?? 0
                );

                if (success)
                {
                    _logger.LogInformation($"✅ Coins credited successfully:");
                    _logger.LogInformation($"   User: {user.Email} (Telegram ID: {telegramUserId})");
                    _logger.LogInformation($"   Package: {packageName}");
                    _logger.LogInformation($"   Coins: {coins}");
                    _logger.LogInformation($"   Duration: {days} days");
                    _logger.LogInformation($"   Price: {payload.Amount} {payload.Currency}");

                    var pendingPayment = await _context.PendingPayments
                        .Where(p => p.TelegramId == telegramUserId && p.Status == "pending")
                        .OrderByDescending(p => p.CreatedAt)
                        .FirstOrDefaultAsync();

                    if (pendingPayment != null)
                    {
                        pendingPayment.Status = "completed";
                        pendingPayment.CompletedAt = DateTime.UtcNow;
                        await _context.SaveChangesAsync();
                        _logger.LogInformation($"✅ Updated pending payment to completed");
                    }

                    return Ok(new
                    {
                        status = "ok",
                        coins_added = coins,
                        user_email = user.Email,
                        package = packageName
                    });
                }
                else
                {
                    _logger.LogError($"❌ Failed to credit coins for user {user.Email}");
                    lock (_lock) { _processedPayments.Remove(paymentKey); }
                    return StatusCode(500, new { error = "Failed to credit coins" });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"❌ Error processing purchase: {ex.Message}");
                lock (_lock) { _processedPayments.Remove(paymentKey); }
                throw;
            }
        }

        private (int coins, int days, string name) DeterminePackageByAmount(decimal amount)
        {
            return amount switch
            {
                2m => (100, 30, "1 месяц"),
                5m => (300, 90, "3 месяца"),
                10m => (600, 180, "6 месяцев"),
                20m => (1200, 365, "Год"),
                _ => (0, 0, "Unknown")
            };
        }

        private bool VerifySignature(string payload, string signature, string secret)
        {
            try
            {
                using var hmac = new System.Security.Cryptography.HMACSHA256(
                    System.Text.Encoding.UTF8.GetBytes(secret));

                var hash = hmac.ComputeHash(System.Text.Encoding.UTF8.GetBytes(payload));
                var computedSignature = Convert.ToHexString(hash).ToLower();

                return computedSignature == signature.ToLower();
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error verifying signature: {ex.Message}");
                return false;
            }
        }
    }

    public class TributeWebhookDto
    {
        public string Name { get; set; } = "";
        public DateTime CreatedAt { get; set; }
        public DateTime SentAt { get; set; }
        public TributePayloadDto? Payload { get; set; }
    }

    public class TributePayloadDto
    {
        public int? ProductId { get; set; }
        public decimal? Amount { get; set; }
        public string? Currency { get; set; }
        public int? UserId { get; set; }
        public long? TelegramUserId { get; set; }
    }
}
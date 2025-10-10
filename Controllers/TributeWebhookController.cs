using Microsoft.AspNetCore.Mvc;
using FitnessTracker.API.Services;
using FitnessTracker.API.Repositories;
using FitnessTracker.API.Data;
using Microsoft.EntityFrameworkCore;

namespace FitnessTracker.API.Controllers
{
    /// <summary>
    /// 💳 УЛУЧШЕННЫЙ Webhook контроллер Tribute с дедупликацией
    /// </summary>
    [ApiController]
    [Route("api/tribute")]
    public class TributeWebhookController : ControllerBase
    {
        private readonly ITributeApiService _tributeService;
        private readonly ILwCoinService _lwCoinService;
        private readonly IUserRepository _userRepository;
        private readonly ApplicationDbContext _context;
        private readonly ILogger<TributeWebhookController> _logger;

        // Кэш обработанных платежей (в продакшене использовать Redis)
        private static readonly HashSet<string> _processedPayments = new();
        private static readonly object _lock = new();

        public TributeWebhookController(
            ITributeApiService tributeService,
            ILwCoinService lwCoinService,
            IUserRepository userRepository,
            ApplicationDbContext context,
            ILogger<TributeWebhookController> logger)
        {
            _tributeService = tributeService;
            _lwCoinService = lwCoinService;
            _userRepository = userRepository;
            _context = context;
            _logger = logger;
        }

        /// <summary>
        /// 💳 Webhook от Tribute (улучшенный с дедупликацией)
        /// </summary>
        [HttpPost("webhook")]
        public async Task<IActionResult> HandleWebhook()
        {
            try
            {
                // 1. Читаем тело запроса
                using var reader = new StreamReader(Request.Body);
                var body = await reader.ReadToEndAsync();

                // 2. Проверяем подпись
                var signature = Request.Headers["X-Tribute-Signature"].FirstOrDefault();

                if (string.IsNullOrEmpty(signature))
                {
                    _logger.LogWarning("⚠️ Webhook without signature");
                    return BadRequest(new { error = "Missing signature" });
                }

                var isValid = await _tributeService.VerifyWebhookSignature(body, signature);
                if (!isValid)
                {
                    _logger.LogWarning("❌ Invalid webhook signature");
                    return Unauthorized(new { error = "Invalid signature" });
                }

                // 3. Парсим данные
                var data = System.Text.Json.JsonSerializer.Deserialize<TributeWebhookData>(body);
                if (data == null || string.IsNullOrEmpty(data.OrderId))
                {
                    _logger.LogWarning("⚠️ Invalid webhook payload");
                    return BadRequest(new { error = "Invalid payload" });
                }

                _logger.LogInformation($"💳 Webhook received: {data.Status} for order {data.OrderId}");

                // 4. ДЕДУПЛИКАЦИЯ - проверяем не обработан ли уже
                lock (_lock)
                {
                    if (_processedPayments.Contains(data.OrderId))
                    {
                        _logger.LogInformation($"⚠️ Order {data.OrderId} already processed (duplicate webhook)");
                        return Ok(new { status = "already_processed" });
                    }
                    _processedPayments.Add(data.OrderId);
                }

                // 5. Обрабатываем только успешные платежи
                if (data.Status == "success" || data.Status == "completed")
                {
                    var success = await ProcessSuccessfulPaymentAsync(data);

                    if (!success)
                    {
                        // Убираем из кэша если не удалось обработать
                        lock (_lock)
                        {
                            _processedPayments.Remove(data.OrderId);
                        }
                    }

                    return Ok(new { status = "ok", processed = success });
                }

                return Ok(new { status = "ok", message = $"Status {data.Status} - no action needed" });
            }
            catch (Exception ex)
            {
                _logger.LogError($"❌ Webhook error: {ex.Message}");
                return StatusCode(500, new { error = "Internal error" });
            }
        }

        /// <summary>
        /// 💰 Обработать успешный платёж
        /// </summary>
        private async Task<bool> ProcessSuccessfulPaymentAsync(TributeWebhookData data)
        {
            try
            {
                // 1. Проверяем наличие telegram_id
                var telegramIdStr = data.Metadata?.TelegramId;
                if (string.IsNullOrEmpty(telegramIdStr))
                {
                    _logger.LogWarning($"⚠️ No telegram_id for order {data.OrderId}");
                    return false;
                }

                if (!long.TryParse(telegramIdStr, out var telegramId))
                {
                    _logger.LogWarning($"⚠️ Invalid telegram_id: {telegramIdStr}");
                    return false;
                }

                // 2. Находим пользователя
                var user = await _userRepository.GetByTelegramIdAsync(telegramId);
                if (user == null)
                {
                    _logger.LogWarning($"⚠️ User not found for Telegram ID {telegramId}");
                    return false;
                }

                // 3. Определяем пакет
                var (coins, days) = DeterminePackageFromAmount(data.Amount);
                if (coins == 0)
                {
                    _logger.LogWarning($"⚠️ Unknown package for amount: {data.Amount}");
                    return false;
                }

                // 4. Начисляем монеты
                var success = await _lwCoinService.PurchaseSubscriptionCoinsAsync(
                    user.Id,
                    coins,
                    days,
                    data.Amount
                );

                if (success)
                {
                    _logger.LogInformation($"✅ Processed webhook: {coins} coins for user {user.Email} (order: {data.OrderId})");

                    // 5. Обновляем pending payment если есть
                    await UpdatePendingPaymentAsync(data.OrderId);
                }

                return success;
            }
            catch (Exception ex)
            {
                _logger.LogError($"❌ Error processing payment: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 📝 Обновить статус pending платежа
        /// </summary>
        private async Task UpdatePendingPaymentAsync(string orderId)
        {
            try
            {
                var payment = await _context.Set<PendingPayment>()
                    .FirstOrDefaultAsync(p => p.PaymentId == orderId);

                if (payment != null && payment.Status == "pending")
                {
                    payment.Status = "completed";
                    payment.CompletedAt = DateTime.UtcNow;
                    await _context.SaveChangesAsync();

                    _logger.LogInformation($"✅ Updated pending payment {orderId} to completed");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"⚠️ Error updating pending payment: {ex.Message}");
            }
        }

        private (int coins, int days) DeterminePackageFromAmount(decimal amount)
        {
            return amount switch
            {
                2m => (100, 30),
                5m => (300, 90),
                10m => (600, 180),
                20m => (1200, 365),
                _ => (0, 0)
            };
        }
    }

    public class TributeWebhookData
    {
        public string OrderId { get; set; } = "";
        public string Status { get; set; } = "";
        public decimal Amount { get; set; }
        public string Currency { get; set; } = "EUR";
        public TributeMetadata? Metadata { get; set; }
    }

    public class TributeMetadata
    {
        public string? TelegramId { get; set; }
        public string? PackageId { get; set; }
    }
}
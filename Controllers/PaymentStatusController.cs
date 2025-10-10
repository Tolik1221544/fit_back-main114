using Microsoft.AspNetCore.Mvc;
using FitnessTracker.API.Services;
using FitnessTracker.API.Repositories;

namespace FitnessTracker.API.Controllers
{
    /// <summary>
    /// 💳 Проверка статуса платежей
    /// </summary>
    [ApiController]
    [Route("api/payment")]
    public class PaymentStatusController : ControllerBase
    {
        private readonly ITributeApiService _tributeService;
        private readonly ILwCoinService _lwCoinService;
        private readonly IUserRepository _userRepository;
        private readonly ILogger<PaymentStatusController> _logger;

        public PaymentStatusController(
            ITributeApiService tributeService,
            ILwCoinService lwCoinService,
            IUserRepository userRepository,
            ILogger<PaymentStatusController> logger)
        {
            _tributeService = tributeService;
            _lwCoinService = lwCoinService;
            _userRepository = userRepository;
            _logger = logger;
        }

        /// <summary>
        /// 🔍 Проверить статус платежа по orderId
        /// </summary>
        [HttpGet("check/{orderId}")]
        public async Task<IActionResult> CheckPaymentStatus(string orderId)
        {
            try
            {
                _logger.LogInformation($"🔍 Checking payment status for order: {orderId}");

                var status = await _tributeService.GetOrderStatusAsync(orderId);

                if (status.Status == "error")
                {
                    return BadRequest(new
                    {
                        success = false,
                        status = "error",
                        message = status.ErrorMessage
                    });
                }

                // Если платёж завершён - обрабатываем
                if (status.Status == "completed" || status.Status == "success")
                {
                    var processed = await ProcessCompletedPayment(status);

                    return Ok(new
                    {
                        success = true,
                        status = status.Status,
                        coins_added = processed,
                        amount = status.Amount,
                        currency = status.Currency,
                        completed_at = status.CompletedAt
                    });
                }

                // Платёж ещё в процессе
                return Ok(new
                {
                    success = true,
                    status = status.Status,
                    coins_added = false,
                    message = GetStatusMessage(status.Status)
                });
            }
            catch (Exception ex)
            {
                _logger.LogError($"❌ Error checking payment: {ex.Message}");
                return StatusCode(500, new
                {
                    success = false,
                    error = "Internal server error"
                });
            }
        }

        /// <summary>
        /// 🔍 Проверить статус по telegram_id (для бота)
        /// </summary>
        [HttpGet("check-by-telegram/{telegramId}")]
        public async Task<IActionResult> CheckByTelegramId(long telegramId)
        {
            try
            {
                _logger.LogInformation($"🔍 Checking payments for Telegram user: {telegramId}");

                // Здесь нужно добавить логику поиска pending платежей в вашей БД
                // Для примера просто возвращаем статус

                return Ok(new
                {
                    success = true,
                    message = "Check specific orderId instead",
                    telegram_id = telegramId
                });
            }
            catch (Exception ex)
            {
                _logger.LogError($"❌ Error: {ex.Message}");
                return StatusCode(500, new { success = false, error = ex.Message });
            }
        }

        /// <summary>
        /// 💰 Обработать завершённый платёж
        /// </summary>
        private async Task<bool> ProcessCompletedPayment(TributeOrderStatus status)
        {
            try
            {
                if (string.IsNullOrEmpty(status.TelegramId))
                {
                    _logger.LogWarning($"⚠️ No telegram_id in order {status.OrderId}");
                    return false;
                }

                if (!long.TryParse(status.TelegramId, out var telegramId))
                {
                    _logger.LogWarning($"⚠️ Invalid telegram_id: {status.TelegramId}");
                    return false;
                }

                var user = await _userRepository.GetByTelegramIdAsync(telegramId);
                if (user == null)
                {
                    _logger.LogWarning($"⚠️ User not found for Telegram ID: {telegramId}");
                    return false;
                }

                // Определяем пакет по сумме
                var (coins, days) = DeterminePackageFromAmount(status.Amount);

                if (coins == 0)
                {
                    _logger.LogWarning($"⚠️ Unknown package for amount: {status.Amount}");
                    return false;
                }

                // Начисляем монеты
                var success = await _lwCoinService.PurchaseSubscriptionCoinsAsync(
                    user.Id,
                    coins,
                    days,
                    status.Amount
                );

                if (success)
                {
                    _logger.LogInformation($"✅ Processed payment {status.OrderId}: {coins} coins for user {user.Email}");
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
        /// 📦 Определить пакет по сумме
        /// </summary>
        private (int coins, int days) DeterminePackageFromAmount(decimal amount)
        {
            return amount switch
            {
                2m => (100, 30),      // 1 месяц
                5m => (300, 90),      // 3 месяца
                10m => (600, 180),    // 6 месяцев
                20m => (1200, 365),   // 1 год
                _ => (0, 0)
            };
        }

        /// <summary>
        /// 📝 Получить описание статуса
        /// </summary>
        private string GetStatusMessage(string status)
        {
            return status switch
            {
                "pending" => "Платёж обрабатывается",
                "completed" => "Платёж успешно завершён",
                "success" => "Платёж успешно завершён",
                "failed" => "Платёж отклонён",
                "cancelled" => "Платёж отменён",
                _ => "Неизвестный статус"
            };
        }
    }
}
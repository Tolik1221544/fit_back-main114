using FitnessTracker.API.DTOs;
using FitnessTracker.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace FitnessTracker.API.Controllers
{
    /// <summary>
    /// 💰 Управление LW Coins и подписками
    /// </summary>
    [ApiController]
    [Route("api/lw-coin")]
    [Authorize]
    public class LwCoinController : ControllerBase
    {
        private readonly ILwCoinService _lwCoinService;

        public LwCoinController(ILwCoinService lwCoinService)
        {
            _lwCoinService = lwCoinService;
        }

        /// <summary>
        /// 💰 Получить баланс LW Coins
        /// </summary>
        /// <returns>Баланс и информация о подписке с уведомлениями</returns>
        /// <response code="200">Баланс успешно получен</response>
        /// <response code="401">Требуется авторизация</response>
        /// <remarks>
        /// ✅ ОБНОВЛЕНО: Теперь включает уведомления о статусе премиум подписки:
        /// - Предупреждения об истечении подписки
        /// - Уведомления о переводе на стандартный план
        /// - Автоматический откат при истечении подписки
        /// </remarks>
        [HttpGet("balance")]
        public async Task<IActionResult> GetBalance()
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized();

                var balance = await _lwCoinService.GetUserLwCoinBalanceAsync(userId);
                return Ok(balance);
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        /// <summary>
        /// 💸 Потратить LW Coins
        /// </summary>
        /// <param name="request">Данные о трате монет</param>
        /// <returns>Результат траты</returns>
        /// <response code="200">Монеты успешно потрачены</response>
        /// <response code="400">Недостаточно монет или неверный запрос</response>
        /// <response code="401">Требуется авторизация</response>
        /// <remarks>
        /// ✅ ОБНОВЛЕНО: Добавлены поля price и period для отслеживания покупок.
        /// Премиум пользователи тратят 0 монет, но использование логируется.
        /// </remarks>
        /// <example>
        /// {
        ///   "amount": 1,
        ///   "type": "pack_50",
        ///   "description": "Food scan",
        ///   "featureUsed": "photo",
        ///   "price": 0.50,
        ///   "period": "one-time"
        /// }
        /// </example>
        [HttpPost("spend")]
        public async Task<IActionResult> SpendCoins([FromBody] SpendLwCoinsRequest request)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized();

                var success = await _lwCoinService.SpendLwCoinsAsync(
                    userId, request.Amount, request.Type, request.Description, request.FeatureUsed);

                if (!success)
                    return BadRequest(new { error = "Insufficient LW Coins or invalid request" });

                return Ok(new { success = true });
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        /// <summary>
        /// 📊 Получить историю транзакций
        /// </summary>
        /// <returns>История транзакций с ценами и периодами</returns>
        /// <response code="200">История успешно получена</response>
        /// <response code="401">Требуется авторизация</response>
        /// <remarks>
        /// ✅ ОБНОВЛЕНО: Транзакции теперь включают информацию о ценах и периодах покупок.
        /// </remarks>
        [HttpGet("transactions")]
        public async Task<IActionResult> GetTransactions()
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized();

                var transactions = await _lwCoinService.GetUserLwCoinTransactionsAsync(userId);
                return Ok(transactions);
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        /// <summary>
        /// 👑 Купить премиум подписку
        /// </summary>
        /// <param name="request">Данные о покупке премиума</param>
        /// <returns>Результат покупки</returns>
        /// <response code="200">Премиум подписка успешно активирована</response>
        /// <response code="400">Ошибка при покупке</response>
        /// <response code="401">Требуется авторизация</response>
        /// <remarks>
        /// ✅ ОБНОВЛЕНО: Теперь поддерживает кастомные цены и периоды.
        /// Автоматически отслеживает дату истечения и отправляет уведомления.
        /// </remarks>
        /// <example>
        /// {
        ///   "paymentTransactionId": "stripe_tx_123",
        ///   "price": 8.99,
        ///   "period": "monthly"
        /// }
        /// </example>
        [HttpPost("purchase-premium")]
        public async Task<IActionResult> PurchasePremium([FromBody] PurchasePremiumRequest request)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized();

                var success = await _lwCoinService.PurchasePremiumAsync(userId, request);
                return Ok(new { success });
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        /// <summary>
        /// 🪙 Купить пакет LW Coins
        /// </summary>
        /// <param name="request">Данные о покупке пакета монет</param>
        /// <returns>Результат покупки</returns>
        /// <response code="200">Пакет монет успешно куплен</response>
        /// <response code="400">Ошибка при покупке</response>
        /// <response code="401">Требуется авторизация</response>
        /// <remarks>
        /// ✅ ОБНОВЛЕНО: Поддерживает кастомные цены для разных пакетов.
        /// </remarks>
        /// <example>
        /// {
        ///   "packType": "pack_50",
        ///   "paymentTransactionId": "stripe_tx_456",
        ///   "price": 0.50,
        ///   "period": "one-time"
        /// }
        /// </example>
        [HttpPost("purchase-coins")]
        public async Task<IActionResult> PurchaseCoins([FromBody] PurchaseCoinPackRequest request)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized();

                var success = await _lwCoinService.PurchaseCoinPackAsync(userId, request);
                return Ok(new { success });
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        /// <summary>
        /// 📊 Проверить лимиты использования
        /// </summary>
        /// <param name="featureType">Тип функции для проверки</param>
        /// <returns>Информация о лимитах пользователя</returns>
        /// <response code="200">Лимиты успешно получены</response>
        /// <response code="401">Требуется авторизация</response>
        [HttpGet("check-limit/{featureType}")]
        public async Task<IActionResult> CheckFeatureLimit(string featureType)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized();

                var limits = await _lwCoinService.GetUserLimitsAsync(userId);
                return Ok(limits);
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        /// <summary>
        /// 💲 Получить прайс-лист
        /// </summary>
        /// <returns>Актуальные цены на подписки и пакеты</returns>
        /// <response code="200">Прайс-лист получен</response>
        /// <remarks>
        /// ✅ ОБНОВЛЕНО: Включает информацию о новых типах подписок и пакетов.
        /// </remarks>
        [HttpGet("pricing")]
        [AllowAnonymous]
        public IActionResult GetPricing()
        {
            return Ok(new
            {
                lwCoinPricing = new
                {
                    photoCost = 1,
                    voiceCost = 1,
                    textCost = 1,
                    exerciseTrackingCost = 0,
                    archiveCost = 0
                },
                subscriptions = new[]
                {
                    new {
                        type = "premium",
                        price = 8.99m,
                        currency = "USD",
                        description = "Unlimited usage",
                        period = "monthly",
                        features = new[] { "Unlimited photo scans", "No ads", "Priority support", "Advanced analytics" }
                    }
                },
                coinPacks = new[]
                {
                    new {
                        type = "pack_50",
                        price = 0.50m,
                        currency = "USD",
                        description = "50 LW Coins",
                        period = "one-time",
                        coins = 50
                    },
                    new {
                        type = "pack_100",
                        price = 1.00m,
                        currency = "USD",
                        description = "100 LW Coins",
                        period = "one-time",
                        coins = 100
                    }
                },
                freeFeatures = new[]
                {
                    "Exercise tracking",
                    "Progress archive",
                    "Basic statistics",
                    "Skin system with XP boost"
                },
                monthlyAllowance = new
                {
                    freeUsers = 300,
                    trialBonus = 150,
                    referralBonus = 150
                }
            });
        }
    }
}
using FitnessTracker.API.DTOs;
using FitnessTracker.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace FitnessTracker.API.Controllers
{
    /// <summary>
    /// 💰 Управление LW Coins и подписками с новой ценовой моделью
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
        /// 💰 Получить баланс LW Coins с новыми дневными лимитами
        /// </summary>
        /// <returns>Баланс и информация о подписке с дневными лимитами</returns>
        /// <response code="200">Баланс успешно получен</response>
        /// <response code="401">Требуется авторизация</response>
        /// <remarks>
        /// - Дневной лимит: 10 монет (300 монет / 30 дней)
        /// - Новые цены: Фото 2.5, Голос 1.5, Текст 1.0 монеты
        /// - Дневное использование и остаток
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
        /// 💸 Потратить LW Coins с новыми ценами
        /// </summary>
        /// <param name="request">Данные о трате монет</param>
        /// <returns>Результат траты</returns>
        /// <response code="200">Монеты успешно потрачены</response>
        /// <response code="400">Недостаточно монет или превышен дневной лимит</response>
        /// <response code="401">Требуется авторизация</response>
        /// <remarks>
        /// - Фото-анализ: 2.5 монеты
        /// - Голосовой ввод: 1.5 монеты  
        /// - Текстовый ввод: 1.0 монета
        /// - Дневной лимит для тарифа "База": 10 монет/день
        /// - Премиум пользователи: без лимитов
        /// </remarks>
        /// <example>
        /// {
        ///   "amount": 1,
        ///   "type": "ai_scan",
        ///   "description": "Food photo analysis",
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
                    return BadRequest(new
                    {
                        error = "Insufficient LW Coins or daily limit exceeded",
                        message = "Недостаточно монет или превышен дневной лимит"
                    });

                return Ok(new { success = true });
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        /// <summary>
        /// 📊 Получить историю транзакций с дробными монетами
        /// </summary>
        /// <returns>История транзакций с новыми ценами</returns>
        /// <response code="200">История успешно получена</response>
        /// <response code="401">Требуется авторизация</response>
        /// <remarks>
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
        /// Премиум подписка снимает все лимиты и дает безлимитное использование AI функций.
        /// </remarks>
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
        /// 📊 Проверить лимиты использования с дневными ограничениями
        /// </summary>
        /// <param name="featureType">Тип функции для проверки</param>
        /// <returns>Информация о лимитах пользователя</returns>
        /// <response code="200">Лимиты успешно получены</response>
        /// <response code="401">Требуется авторизация</response>
        /// <remarks>
        /// ✅ НОВОЕ: Включает информацию о дневных лимитах для тарифа "База".
        /// </remarks>
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
        /// 💲 Получить обновленный прайс-лист
        /// </summary>
        /// <returns>Актуальные цены с новой ценовой моделью</returns>
        /// <response code="200">Прайс-лист получен</response>
        /// <remarks>
        /// ✅ ОБНОВЛЕНО: Новая ценовая модель согласно требованиям заказчика.
        /// </remarks>
        [HttpGet("pricing")]
        [AllowAnonymous]
        public IActionResult GetPricing()
        {
            return Ok(new
            {
                lwCoinPricing = new
                {
                    photoCost = 2.5m,        // Фото-анализ: 2.5 монеты
                    voiceCost = 1.5m,        // Голосовой ввод: 1.5 монеты
                    textCost = 1.0m,         // Текстовый ввод: 1.0 монета
                    exerciseTrackingCost = 0,
                    archiveCost = 0,
                    bodyAnalysisCost = 0     // Анализ тела остается бесплатным
                },

                // ✅ НОВЫЕ ДНЕВНЫЕ ЛИМИТЫ
                dailyLimits = new
                {
                    baseUserDailyLimit = 10.0m,  // 300 монет / 30 дней = 10 монет/день
                    baseDailyUsageExample = new
                    {
                        photos = 3,     // 3 фото * 2.5 = 7.5 монеты
                        voice = 1,      // 1 голос * 1.5 = 1.5 монеты  
                        text = 2,       // 2 текста * 1.0 = 2.0 монеты
                        total = 11.0m   // Итого: 11 монет (чуть больше лимита)
                    },
                    optimizedDailyUsage = new
                    {
                        photos = 3,     // 3 фото * 2.5 = 7.5 монеты
                        voice = 1,      // 1 голос * 1.5 = 1.5 монеты
                        text = 1,       // 1 текст * 1.0 = 1.0 монета
                        total = 10.0m   // Итого: 10 монет (точно в лимите)
                    }
                },

                subscriptions = new[]
                {
                    new {
                        type = "premium",
                        price = 8.99m,
                        currency = "USD",
                        description = "Unlimited usage - no daily limits",
                        period = "monthly",
                        features = new[] {
                            "Unlimited photo scans",
                            "Unlimited voice input",
                            "Unlimited text analysis",
                            "No daily limits",
                            "Priority support",
                            "Advanced analytics"
                        }
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
                        coins = 50,
                        additionalDays = "5 дней с новыми ценами"
                    },
                    new {
                        type = "pack_100",
                        price = 1.00m,
                        currency = "USD",
                        description = "100 LW Coins",
                        period = "one-time",
                        coins = 100,
                        additionalDays = "10 дней с новыми ценами"
                    }
                },
                freeFeatures = new[]
                {
                    "Exercise tracking",
                    "Progress archive",
                    "Basic statistics",
                    "Skin system with XP boost",
                    "Body analysis (unlimited)", // Остается бесплатным
                    "Weekly body scans"
                },
                monthlyAllowance = new
                {
                    freeUsers = 300,
                    trialBonus = 150,
                    referralBonus = 150,
                    dailyEquivalent = 10.0m  // 300 / 30 = 10 монет/день
                },

                // ✅ НОВАЯ СЕКЦИЯ: Экономическая модель
                economicModel = new
                {
                    targetDailyUsage = new
                    {
                        photos = 3,
                        voice = 1,
                        text = 2,
                        totalCost = 11.0m,
                        note = "Пользователь тарифа 'База' может делать 3 фото, 1 голос и 2 текста в день"
                    },
                    costBreakdown = new
                    {
                        photoAnalysis = "2.5 монеты за анализ фото еды",
                        voiceInput = "1.5 монеты за голосовой ввод тренировки/питания",
                        textAnalysis = "1.0 монета за текстовый анализ",
                        bodyAnalysis = "0 монет - бесплатно для всех",
                        exerciseTracking = "0 монет - бесплатно для всех"
                    }
                }
            });
        }

        /// <summary>
        /// 🔄 Принудительное обновление месячного пополнения
        /// </summary>
        /// <returns>Результат обновления</returns>
        /// <response code="200">Пополнение выполнено</response>
        /// <response code="401">Требуется авторизация</response>
        [HttpPost("force-refill")]
        public async Task<IActionResult> ForceMonthlyRefill()
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized();

                var success = await _lwCoinService.ProcessMonthlyRefillAsync(userId);
                return Ok(new
                {
                    success,
                    message = success ? "Monthly refill processed" : "Refill not due yet"
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }
    }
}
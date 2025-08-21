using FitnessTracker.API.DTOs;
using FitnessTracker.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace FitnessTracker.API.Controllers
{
    /// <summary>
    /// 💰 Управление LW Coins и подписками - Тратьте монеты без ограничений!
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
        /// <returns>Баланс и информация о подписке</returns>
        /// <response code="200">Баланс успешно получен</response>
        /// <response code="401">Требуется авторизация</response>
        /// <remarks>
        /// - Тратьте монеты пока они есть на балансе
        /// - Премиум пользователи: безлимитное использование ИИ
        /// - Цены: Фото 2.5, Голос 1.5, Текст 1.0 монеты
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
        /// <response code="400">Недостаточно монет на балансе</response>
        /// <response code="401">Требуется авторизация</response>
        /// <remarks>
        /// - Фото-анализ: 2.5 монеты
        /// - Голосовой ввод: 1.5 монеты  
        /// - Текстовый ввод: 1.0 монета
        /// - Тратьте сколько угодно, пока есть баланс
        /// - Премиум пользователи: без ограничений
        /// </remarks>
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
                        error = "Insufficient LW Coins",
                        message = "Недостаточно монет на балансе. Купите больше монет или оформите премиум подписку."
                    });

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
        /// <returns>История транзакций с дробными монетами</returns>
        /// <response code="200">История успешно получена</response>
        /// <response code="401">Требуется авторизация</response>
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
        /// Премиум подписка дает безлимитное использование всех AI функций без трат монет.
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
        /// 💲 Получить обновленный прайс-лист
        /// </summary>
        /// <returns>Актуальные цены с новой ценовой моделью</returns>
        /// <response code="200">Прайс-лист получен</response>
        [HttpGet("pricing")]
        [AllowAnonymous]
        public IActionResult GetPricing()
        {
            return Ok(new
            {
                lwCoinPricing = new
                {
                    photoCost = 2.5m,       
                    voiceCost = 1.5m,        
                    textCost = 1.0m,         
                    exerciseTrackingCost = 1.0m,
                    bodyAnalysisCost = 0     
                },

                statisticalLimits = new
                {
                    averageDailyUsage = 10.0m,
                    baseDailyUsageExample = new
                    {
                        photos = 3,   
                        voiceWorkouts = 1,      
                        text = 2,     
                        total = 11.0m,
                        note = "Анализ тренировки = голосовой ввод тренировки (1.5 монеты)"
                    },
                    optimizedDailyUsage = new
                    {
                        photos = 3,
                        voiceWorkouts = 1,    
                        text = 1,
                        total = 10.0m,
                        note = "Рекомендуемое распределение трат для экономии монет"
                    }
                },

                subscriptions = new[]
                {
                    new {
                        type = "premium",
                        price = 8.99m,
                        currency = "USD",
                        description = "Unlimited usage - no limits at all",
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
                        additionalDays = "Примерно 5 дополнительных дней использования"
                    },
                    new {
                        type = "pack_100",
                        price = 1.00m,
                        currency = "USD",
                        description = "100 LW Coins",
                        period = "one-time",
                        coins = 100,
                        additionalDays = "Примерно 10 дополнительных дней использования"
                    }
                },
                freeFeatures = new[]
                {
                    "Exercise tracking",
                    "Basic statistics",
                    "Skin system with XP boost",
                    "Body analysis (unlimited)",
                    "Weekly body scans"
                },
                monthlyAllowance = new
                {
                    freeUsers = 300,
                    trialBonus = 150,
                    referralBonus = 150,
                    averageDailyEquivalent = 10.0m,  
                    note = "Месячное пополнение 300 монет - тратьте как хотите в течение месяца"
                },

                economicModel = new
                {
                    philosophyTitle = "Свобода трат - ваши монеты, ваш выбор",
                    philosophy = "Пользователи должны иметь возможность тратить свои монеты когда угодно и сколько угодно. Никаких дневных блокировок!",
                    targetDailyUsage = new
                    {
                        photos = 3,
                        voiceWorkouts = 1,    
                        text = 2,
                        totalCost = 11.0m,
                        note = "Средний пользователь тарифа 'База' делает примерно столько операций в день"
                    },
                    costBreakdown = new
                    {
                        photoAnalysis = "2.5 монеты за анализ фото еды",
                        voiceWorkoutAnalysis = "1.5 монеты за голосовой анализ тренировки",   
                        voiceFoodAnalysis = "1.5 монеты за голосовой анализ питания",
                        textAnalysis = "1.0 монета за текстовый анализ",
                        bodyAnalysis = "0 монет - бесплатно для всех",                       
                        exerciseTracking = "0 монет - бесплатно для всех"
                    },
                    flexibilityNote = "Хотите потратить все 300 монет за один день? Пожалуйста! Хотите растянуть на месяц? Тоже отлично!"
                }
            });
        }

        /// <summary>
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
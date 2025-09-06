using FitnessTracker.API.DTOs;
using FitnessTracker.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace FitnessTracker.API.Controllers
{
    /// <summary>
    /// 💰 Управление LW Coins - основной контроллер для монет
    /// </summary>
    [ApiController]
    [Route("api/lw-coin")]
    [Authorize]
    public class LwCoinController : ControllerBase
    {
        private readonly ILwCoinService _lwCoinService;
        private readonly ILogger<LwCoinController> _logger;

        public LwCoinController(ILwCoinService lwCoinService, ILogger<LwCoinController> logger)
        {
            _lwCoinService = lwCoinService;
            _logger = logger;
        }

        /// <summary>
        /// 💰 Получить баланс LW Coins с детализацией по типам
        /// </summary>
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
                _logger.LogError($"❌ Error getting balance: {ex.Message}");
                return BadRequest(new { error = ex.Message });
            }
        }

        /// <summary>
        /// 💸 Установить баланс монет (админская функция)
        /// </summary>
        /// <param name="request">Количество монет и источник</param>
        /// <returns>Результат установки баланса</returns>
        [HttpPost("set-balance")]
        [ProducesResponseType(200)]
        [ProducesResponseType(400)]
        public async Task<IActionResult> SetBalance([FromBody] SetBalanceRequest request)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized();

                if (request.Amount < 0)
                    return BadRequest(new { error = "Amount cannot be negative" });

                var success = await _lwCoinService.SetUserCoinsAsync(userId, request.Amount, request.Source ?? "manual");

                if (success)
                {
                    _logger.LogInformation($"💰 Balance set for user {userId}: {request.Amount} coins");
                    return Ok(new
                    {
                        success = true,
                        message = $"Balance set to {request.Amount} coins",
                        newBalance = request.Amount
                    });
                }

                return BadRequest(new { error = "Failed to set balance" });
            }
            catch (Exception ex)
            {
                _logger.LogError($"❌ Error setting balance: {ex.Message}");
                return BadRequest(new { error = ex.Message });
            }
        }

        /// <summary>
        /// 🎁 Купить подписку с монетами (автоматическое удаление по истечении)
        /// </summary>
        /// <param name="request">Данные подписки</param>
        [HttpPost("purchase-subscription")]
        [ProducesResponseType(200)]
        [ProducesResponseType(400)]
        public async Task<IActionResult> PurchaseSubscription([FromBody] PurchaseSubscriptionRequest request)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized();

                if (request.CoinsAmount <= 0)
                    return BadRequest(new { error = "Coins amount must be positive" });

                if (request.DurationDays <= 0)
                    return BadRequest(new { error = "Duration must be positive" });

                var success = await _lwCoinService.PurchaseSubscriptionCoinsAsync(
                    userId,
                    request.CoinsAmount,
                    request.DurationDays,
                    request.Price);

                if (success)
                {
                    var expiryDate = DateTime.UtcNow.AddDays(request.DurationDays);
                    return Ok(new
                    {
                        success = true,
                        message = $"Subscription activated: {request.CoinsAmount} coins for {request.DurationDays} days",
                        coinsAdded = request.CoinsAmount,
                        expiresAt = expiryDate,
                        autoRemovalDate = expiryDate
                    });
                }

                return BadRequest(new { error = "Failed to purchase subscription" });
            }
            catch (Exception ex)
            {
                _logger.LogError($"❌ Error purchasing subscription: {ex.Message}");
                return BadRequest(new { error = ex.Message });
            }
        }

        /// <summary>
        /// 💸 Потратить LW Coins (с учетом новых цен)
        /// </summary>
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
                        message = "Недостаточно монет на балансе"
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
        /// 💲 Получить актуальные цены
        /// </summary>
        [HttpGet("pricing")]
        [AllowAnonymous]
        public IActionResult GetPricing()
        {
            return Ok(new
            {
                actionPricing = new
                {
                    foodScan = new
                    {
                        photo = 1.0m,
                        voice = 1.0m,
                        text = 0.0m,
                        correction = 0.0m
                    },
                    workoutAnalysis = new
                    {
                        voice = 1.0m,
                        text = 0.0m
                    },
                    bodyAnalysis = 0.0m
                },
                bonuses = new
                {
                    registration = 50,
                    referral = 150,
                    referralLevel2 = 75
                },
                subscriptions = new[]
                {
                    new { coins = 50, days = 7, price = 0.99m },
                    new { coins = 100, days = 14, price = 1.99m },
                    new { coins = 200, days = 30, price = 3.99m },
                    new { coins = 500, days = 30, price = 7.99m }
                },
                permanentPacks = new[]
                {
                    new { coins = 50, price = 0.99m },
                    new { coins = 100, price = 1.99m },
                    new { coins = 200, price = 3.99m },
                    new { coins = 500, price = 8.99m }
                },
                premium = new
                {
                    monthlyPrice = 8.99m,
                    features = new[]
                    {
                        "Unlimited AI features",
                        "No coin limits",
                        "Priority support",
                        "Advanced analytics"
                    }
                }
            });
        }
    }
}
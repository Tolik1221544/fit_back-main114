using FitnessTracker.API.DTOs;
using FitnessTracker.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace FitnessTracker.API.Controllers
{
    /// <summary>
    /// 💰 Управление монетами - установка баланса, подписки и автосписание
    /// </summary>
    [ApiController]
    [Route("api/coins")]
    [Authorize]
    [Produces("application/json")]
    public class CoinManagementController : ControllerBase
    {
        private readonly ILwCoinService _lwCoinService;
        private readonly ILogger<CoinManagementController> _logger;

        public CoinManagementController(
            ILwCoinService lwCoinService,
            ILogger<CoinManagementController> logger)
        {
            _lwCoinService = lwCoinService;
            _logger = logger;
        }

        [HttpPost("set-balance")]
        [ProducesResponseType(200)]
        [ProducesResponseType(400)]
        [ProducesResponseType(401)]
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

        [HttpPost("purchase-subscription")]
        [ProducesResponseType(200)]
        [ProducesResponseType(400)]
        [ProducesResponseType(401)]
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

        [HttpGet("balance-details")]
        [ProducesResponseType(200)]
        [ProducesResponseType(401)]
        public async Task<IActionResult> GetBalanceDetails()
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
                _logger.LogError($"❌ Error getting balance details: {ex.Message}");
                return BadRequest(new { error = ex.Message });
            }
        }

        [HttpGet("pricing")]
        [AllowAnonymous]
        [ProducesResponseType(200)]
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
                    new { coins = 50, days = 7, suggestedPrice = 0.99m },
                    new { coins = 100, days = 14, suggestedPrice = 1.99m },
                    new { coins = 200, days = 30, suggestedPrice = 3.99m },
                    new { coins = 500, days = 30, suggestedPrice = 7.99m }
                },
                permanentPacks = new[]
                {
                    new { coins = 50, suggestedPrice = 0.99m },
                    new { coins = 100, suggestedPrice = 1.99m },
                    new { coins = 200, suggestedPrice = 3.99m },
                    new { coins = 500, suggestedPrice = 8.99m }
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

    public class SetBalanceRequest
    {
        public decimal Amount { get; set; }
        public string? Source { get; set; } = "manual";
    }

    public class PurchaseSubscriptionRequest
    {
        public int CoinsAmount { get; set; }
        public int DurationDays { get; set; }
        public decimal Price { get; set; }
    }
}
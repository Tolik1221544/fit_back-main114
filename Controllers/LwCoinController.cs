using FitnessTracker.API.DTOs;
using FitnessTracker.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace FitnessTracker.API.Controllers
{
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

        [HttpGet("pricing")]
        [AllowAnonymous]
        public IActionResult GetPricing() // ✅ УБРАНО: async Task<IActionResult>
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
            new { type = "premium", price = 8.99m, currency = "USD", description = "Unlimited usage", period = "monthly" },
            new { type = "pack_50", price = 0.50m, currency = "USD", description = "50 LW Coins", period = "one-time" },
            new { type = "pack_100", price = 1.00m, currency = "USD", description = "100 LW Coins", period = "one-time" }
        },
                freeFeatures = new[]
                {
            "Exercise tracking",
            "Progress archive",
            "Basic statistics"
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
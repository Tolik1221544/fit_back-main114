using FitnessTracker.API.DTOs;
using FitnessTracker.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace FitnessTracker.API.Controllers
{
    /// <summary>
    /// 💳 Верификация покупок из Google Play и App Store
    /// </summary>
    [ApiController]
    [Route("api/purchase")]
    [Authorize]
    public class PurchaseController : ControllerBase
    {
        private readonly IGooglePlayPurchaseService _googlePlayService;
        private readonly IApplePurchaseService _appleService;
        private readonly ILogger<PurchaseController> _logger;

        public PurchaseController(
            IGooglePlayPurchaseService googlePlayService,
            IApplePurchaseService appleService,
            ILogger<PurchaseController> logger)
        {
            _googlePlayService = googlePlayService;
            _appleService = appleService;
            _logger = logger;
        }

        /// <summary>
        /// 🤖 Верификация покупки Google Play
        /// </summary>
        [HttpPost("verify/google")]
        [ProducesResponseType(typeof(PurchaseVerificationResponse), 200)]
        [ProducesResponseType(400)]
        [ProducesResponseType(401)]
        public async Task<IActionResult> VerifyGooglePurchase([FromBody] VerifyGooglePurchaseRequest request)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized(new { error = "User not authenticated" });

                _logger.LogInformation($"🤖 Google Play verification: {request.ProductId} for user {userId}");

                var result = await _googlePlayService.VerifyPurchaseAsync(userId, request);

                if (result.Success)
                {
                    _logger.LogInformation($"✅ Verified! Coins: {result.CoinsAdded}, Balance: {result.NewBalance}");
                }

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError($"❌ Google verification error: {ex.Message}");
                return BadRequest(new PurchaseVerificationResponse
                {
                    Success = false,
                    Status = "error",
                    Message = "Verification failed",
                    ErrorDetails = ex.Message
                });
            }
        }

        /// <summary>
        /// 🍎 Верификация покупки App Store
        /// </summary>
        [HttpPost("verify/apple")]
        [ProducesResponseType(typeof(PurchaseVerificationResponse), 200)]
        [ProducesResponseType(400)]
        [ProducesResponseType(401)]
        public async Task<IActionResult> VerifyApplePurchase([FromBody] VerifyApplePurchaseRequest request)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized(new { error = "User not authenticated" });

                _logger.LogInformation($"🍎 Apple verification: {request.ProductId} for user {userId}");

                var result = await _appleService.VerifyPurchaseAsync(userId, request);

                if (result.Success)
                {
                    _logger.LogInformation($"✅ Verified! Coins: {result.CoinsAdded}, Balance: {result.NewBalance}");
                }

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError($"❌ Apple verification error: {ex.Message}");
                return BadRequest(new PurchaseVerificationResponse
                {
                    Success = false,
                    Status = "error",
                    Message = "Verification failed",
                    ErrorDetails = ex.Message
                });
            }
        }

        /// <summary>
        /// 🔄 Восстановление покупок
        /// </summary>
        [HttpPost("restore")]
        [ProducesResponseType(typeof(RestorePurchasesResponse), 200)]
        public async Task<IActionResult> RestorePurchases([FromBody] RestorePurchasesRequest request)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized();

                _logger.LogInformation($"🔄 Restore purchases: {request.Platform} for user {userId}");

                RestorePurchasesResponse result;

                if (request.Platform.ToLower() == "google")
                {
                    if (request.PurchaseTokens == null || !request.PurchaseTokens.Any())
                        return BadRequest(new { error = "Purchase tokens required" });

                    result = await _googlePlayService.RestorePurchasesAsync(userId, request.PurchaseTokens);
                }
                else if (request.Platform.ToLower() == "apple")
                {
                    if (string.IsNullOrEmpty(request.ReceiptData))
                        return BadRequest(new { error = "Receipt data required" });

                    result = await _appleService.RestorePurchasesAsync(userId, request.ReceiptData);
                }
                else
                {
                    return BadRequest(new { error = "Invalid platform. Use 'google' or 'apple'" });
                }

                _logger.LogInformation($"✅ Restored {result.RestoredCount} purchases, {result.TotalCoinsRestored} coins");

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError($"❌ Restore error: {ex.Message}");
                return BadRequest(new { error = ex.Message });
            }
        }

        /// <summary>
        /// 📊 Статус подписки
        /// </summary>
        [HttpGet("subscription/status")]
        public async Task<IActionResult> GetSubscriptionStatus([FromQuery] string? platform = null)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized();

                if (string.IsNullOrEmpty(platform))
                {
                    var googleSub = await _googlePlayService.GetSubscriptionStatusAsync(userId);
                    var appleSub = await _appleService.GetSubscriptionStatusAsync(userId);

                    return Ok(new
                    {
                        hasActiveSubscription = googleSub.HasActiveSubscription || appleSub.HasActiveSubscription,
                        google = googleSub,
                        apple = appleSub
                    });
                }

                if (platform.ToLower() == "google")
                {
                    var status = await _googlePlayService.GetSubscriptionStatusAsync(userId);
                    return Ok(status);
                }
                else if (platform.ToLower() == "apple")
                {
                    var status = await _appleService.GetSubscriptionStatusAsync(userId);
                    return Ok(status);
                }

                return BadRequest(new { error = "Invalid platform" });
            }
            catch (Exception ex)
            {
                _logger.LogError($"❌ Subscription status error: {ex.Message}");
                return BadRequest(new { error = ex.Message });
            }
        }
    }
}
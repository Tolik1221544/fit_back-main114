using FitnessTracker.API.DTOs;
using FitnessTracker.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace FitnessTracker.API.Controllers
{
    [ApiController]
    [Route("api/referral")]
    [Authorize]
    public class ReferralController : ControllerBase
    {
        private readonly IReferralService _referralService;

        public ReferralController(IReferralService referralService)
        {
            _referralService = referralService;
        }

        [HttpPost("set")]
        public async Task<IActionResult> SetReferral([FromBody] SetReferralRequest request)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized();

                var result = await _referralService.SetReferralAsync(userId, request);
                return Ok(new { success = result });
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        [HttpGet("generate")]
        public async Task<IActionResult> GenerateReferralCode()
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized();

                var result = await _referralService.GenerateReferralLinkAsync(userId);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        [HttpGet("stats")]
        public async Task<IActionResult> GetReferralStats()
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized();

                var stats = await _referralService.GetReferralStatsAsync(userId);
                return Ok(stats);
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        [HttpGet("validate/{referralCode}")]
        [AllowAnonymous]
        public async Task<IActionResult> ValidateReferralCode(string referralCode)
        {
            try
            {
                var result = await _referralService.ValidateReferralCodeAsync(referralCode);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }
    }
}
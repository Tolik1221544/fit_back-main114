using FitnessTracker.API.DTOs;
using FitnessTracker.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace FitnessTracker.API.Controllers
{
    [ApiController]
    [Route("api/referral")]
    public class ReferralController : ControllerBase
    {
        private readonly IReferralService _referralService;

        public ReferralController(IReferralService referralService)
        {
            _referralService = referralService;
        }

        [HttpPost("set")]
        [Authorize]
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
        [Authorize]
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

        [HttpGet("count")]
        [Authorize]
        public async Task<IActionResult> GetReferralCount()
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized();

                var count = await _referralService.GetReferralCountAsync(userId);
                return Ok(new { count });
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        [HttpGet("stats")]
        [Authorize]
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

        [HttpGet("leaderboard")]
        [Authorize]
        public async Task<IActionResult> GetLeaderboard()
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized();

                var stats = await _referralService.GetReferralStatsAsync(userId);
                return Ok(new { leaderboard = stats.Leaderboard });
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        [HttpPost("validate")]
        [AllowAnonymous]
        public async Task<IActionResult> ValidateReferralCode([FromBody] ValidateReferralRequest request)
        {
            try
            {
                var result = await _referralService.ValidateReferralCodeAsync(request.ReferralCode);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        [HttpGet("my-code")]
        [Authorize]
        public async Task<IActionResult> GetMyReferralCode()
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized();

                var code = await _referralService.GenerateReferralCodeAsync(userId);
                return Ok(new { referralCode = code });
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        [HttpGet("levels")]
        [Authorize]
        public async Task<IActionResult> GetReferralLevels()
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized();

                var stats = await _referralService.GetReferralStatsAsync(userId);

                var levels = new
                {
                    firstLevel = new
                    {
                        count = stats.FirstLevelReferrals.Count,
                        totalCoins = stats.FirstLevelReferrals.Sum(r => r.RewardCoins),
                        users = stats.FirstLevelReferrals.Select(r => new
                        {
                            name = r.Name,
                            email = r.Email,
                            level = r.Level,
                            joinedAt = r.JoinedAt,
                            rewardCoins = r.RewardCoins,
                            isPremium = r.IsPremium,
                            status = r.Status
                        })
                    },
                    secondLevel = new
                    {
                        count = stats.SecondLevelReferrals.Count,
                        totalCoins = stats.SecondLevelReferrals.Sum(r => r.RewardCoins),
                        users = stats.SecondLevelReferrals.Select(r => new
                        {
                            name = r.Name,
                            email = r.Email,
                            level = r.Level,
                            joinedAt = r.JoinedAt,
                            rewardCoins = r.RewardCoins,
                            isPremium = r.IsPremium,
                            status = r.Status
                        })
                    },
                    summary = new
                    {
                        totalReferrals = stats.TotalReferrals,
                        totalEarnedCoins = stats.TotalEarnedCoins,
                        monthlyReferrals = stats.MonthlyReferrals,
                        monthlyEarnedCoins = stats.MonthlyEarnedCoins,
                        rank = stats.Rank
                    }
                };

                return Ok(levels);
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        [HttpGet("earnings")]
        [Authorize]
        public async Task<IActionResult> GetReferralEarnings()
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized();

                var stats = await _referralService.GetReferralStatsAsync(userId);

                var earnings = new
                {
                    total = new
                    {
                        coins = stats.TotalEarnedCoins,
                        referrals = stats.TotalReferrals,
                        breakdown = new
                        {
                            firstLevel = stats.FirstLevelReferrals.Sum(r => r.RewardCoins),
                            secondLevel = stats.SecondLevelReferrals.Sum(r => r.RewardCoins)
                        }
                    },
                    monthly = new
                    {
                        coins = stats.MonthlyEarnedCoins,
                        referrals = stats.MonthlyReferrals,
                        breakdown = new
                        {
                            firstLevel = stats.FirstLevelReferrals.Where(r => r.JoinedAt.Month == DateTime.UtcNow.Month).Sum(r => r.RewardCoins),
                            secondLevel = stats.SecondLevelReferrals.Where(r => r.JoinedAt.Month == DateTime.UtcNow.Month).Sum(r => r.RewardCoins)
                        }
                    },
                    rewardStructure = new
                    {
                        firstLevelReward = 150,
                        secondLevelReward = 75,
                        description = "1-й уровень: 150 LW Coins, 2-й уровень: 75 LW Coins (50% от 1-го уровня)"
                    }
                };

                return Ok(earnings);
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        [HttpGet("qr/{referralCode}")]
        [AllowAnonymous]
        public async Task<IActionResult> GetQrCode(string referralCode)
        {
            try
            {
                // Generate QR code for referral link
                var referralLink = $"https://your-app.com/join?ref={referralCode}";

                // In a real implementation, you would generate an actual QR code image
                // For now, returning a placeholder response
                return Ok(new
                {
                    referralCode = referralCode,
                    referralLink = referralLink,
                    qrCodeData = $"data:image/svg+xml;base64,{Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes($"<svg>QR Code for {referralCode}</svg>"))}"
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }
    }
}
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using FitnessTracker.API.Repositories;

namespace FitnessTracker.API.Controllers
{
    /// <summary>
    /// 🔧 Контроллер для отладки токенов и пользователей
    /// </summary>
    [ApiController]
    [Route("api/debug")]
    public class DebugController : ControllerBase
    {
        private readonly IUserRepository _userRepository;
        private readonly ILogger<DebugController> _logger;

        public DebugController(IUserRepository userRepository, ILogger<DebugController> logger)
        {
            _userRepository = userRepository;
            _logger = logger;
        }

        /// <summary>
        /// 🔍 Проверить токен и пользователя
        /// </summary>
        [HttpGet("token-check")]
        [Authorize]
        public async Task<IActionResult> CheckToken()
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                var userIdClaim = User.FindFirst("user_id")?.Value;
                var jti = User.FindFirst("jti")?.Value;
                var exp = User.FindFirst("exp")?.Value;

                _logger.LogInformation($"Token check - UserId: {userId}, UserIdClaim: {userIdClaim}");

                if (string.IsNullOrEmpty(userId))
                {
                    return BadRequest(new
                    {
                        error = "No user ID in token",
                        claims = User.Claims.Select(c => new { c.Type, c.Value }).ToList()
                    });
                }

                // Проверяем существование пользователя
                var user = await _userRepository.GetByIdAsync(userId);

                return Ok(new
                {
                    tokenValid = true,
                    userId = userId,
                    userIdClaim = userIdClaim,
                    jti = jti,
                    expiry = exp,
                    userExists = user != null,
                    userEmail = user?.Email,
                    userName = user?.Name,
                    allClaims = User.Claims.Select(c => new { c.Type, c.Value }).ToList()
                });
            }
            catch (Exception ex)
            {
                _logger.LogError($"Token check error: {ex.Message}");
                return BadRequest(new { error = ex.Message });
            }
        }

        /// <summary>
        /// 👤 Проверить конкретного пользователя
        /// </summary>
        [HttpGet("user-check/{userId}")]
        public async Task<IActionResult> CheckUser(string userId)
        {
            try
            {
                var user = await _userRepository.GetByIdAsync(userId);

                if (user == null)
                {
                    return NotFound(new { error = "User not found", userId });
                }

                return Ok(new
                {
                    userExists = true,
                    user = new
                    {
                        user.Id,
                        user.Email,
                        user.Name,
                        user.Level,
                        user.Experience,
                        user.LwCoins,
                        user.JoinedAt,
                        user.IsEmailConfirmed
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError($"User check error: {ex.Message}");
                return BadRequest(new { error = ex.Message });
            }
        }
    }
}
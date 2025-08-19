using FitnessTracker.API.DTOs;
using FitnessTracker.API.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;

namespace FitnessTracker.API.Controllers
{
    [ApiController]
    [Route("api/auth")]
    public class AuthController : ControllerBase
    {
        private readonly IAuthService _authService;
        private readonly IGoogleAuthService _googleAuthService;
        private readonly ILogger<AuthController> _logger;

        public AuthController(
            IAuthService authService,
            IGoogleAuthService googleAuthService,
            ILogger<AuthController> logger)
        {
            _authService = authService;
            _googleAuthService = googleAuthService;
            _logger = logger;
        }

        [HttpPost("send-code")]
        public async Task<IActionResult> SendVerificationCode([FromBody] SendVerificationCodeRequest request)
        {
            try
            {
                var result = await _authService.SendVerificationCodeAsync(request.Email);
                return Ok(new
                {
                    success = result,
                    message = result ? "Verification code sent successfully" : "Failed to send code",
                    email = request.Email
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        [HttpPost("confirm-email")]
        public async Task<IActionResult> ConfirmEmail([FromBody] ConfirmEmailRequest request)
        {
            try
            {
                var result = await _authService.ConfirmEmailAsync(request.Email, request.Code);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        /// <summary>
        /// Google OAuth authentication with ID token and server auth code support
        /// </summary>
        /// <param name="request">Google authentication data</param>
        /// <returns>Authentication result with JWT token</returns>
        /// <response code="200">Authentication successful</response>
        /// <response code="400">Invalid data or Google token validation error</response>
        /// <response code="401">Token is invalid</response>
        [HttpPost("google")]
        [ProducesResponseType(typeof(AuthResponseDto), 200)]
        [ProducesResponseType(400)]
        [ProducesResponseType(401)]
        public async Task<IActionResult> GoogleAuth([FromBody] GoogleAuthRequest request)
        {
            try
            {
                if (string.IsNullOrEmpty(request.IdToken) && string.IsNullOrEmpty(request.ServerAuthCode))
                {
                    return BadRequest(new { error = "Either idToken or serverAuthCode is required" });
                }

                AuthResponseDto result;

                if (!string.IsNullOrEmpty(request.IdToken))
                {
                    // Authentication via ID token
                    _logger.LogInformation("Processing Google ID token authentication");
                    result = await _googleAuthService.AuthenticateWithIdTokenAsync(request.IdToken);
                }
                else
                {
                    // Authentication via server auth code
                    _logger.LogInformation("Processing Google server auth code authentication");
                    result = await _googleAuthService.AuthenticateWithServerCodeAsync(request.ServerAuthCode!);
                }

                return Ok(result);
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogWarning($"Google authentication failed: {ex.Message}");
                return Unauthorized(new { error = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError($"Google authentication error: {ex.Message}");
                return BadRequest(new { error = ex.Message });
            }
        }

        [HttpPost("logout")]
        public async Task<IActionResult> Logout([FromBody] LogoutRequest request)
        {
            try
            {
                var result = await _authService.LogoutAsync(request.AccessToken);
                return Ok(new { success = result });
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        /// <summary>
        /// Validate current token (for debugging)
        /// </summary>
        [HttpGet("validate-token")]
        [Authorize]
        public IActionResult ValidateToken()
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                var email = User.FindFirst(ClaimTypes.Email)?.Value;

                return Ok(new
                {
                    valid = true,
                    userId = userId,
                    email = email,
                    claims = User.Claims.Select(c => new { c.Type, c.Value }).ToList()
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }
    }
}
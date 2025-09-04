using FitnessTracker.API.DTOs;
using FitnessTracker.API.Services;
using FitnessTracker.API.Models;
using FitnessTracker.API.Repositories;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using AutoMapper;

namespace FitnessTracker.API.Controllers
{
    [ApiController]
    [Route("api/auth")]
    public class AuthController : ControllerBase
    {
        private readonly IAuthService _authService;
        private readonly IGoogleAuthService _googleAuthService;
        private readonly IAppleAuthService _appleAuthService; 
        private readonly IUserRepository _userRepository;
        private readonly IActivityService _activityService;
        private readonly IFoodIntakeService _foodIntakeService;
        private readonly IMapper _mapper;
        private readonly ILogger<AuthController> _logger;

        public AuthController(
            IAuthService authService,
            IGoogleAuthService googleAuthService,
            IAppleAuthService appleAuthService, 
            IUserRepository userRepository,
            IActivityService activityService,
            IFoodIntakeService foodIntakeService,
            IMapper mapper,
            ILogger<AuthController> logger)
        {
            _authService = authService;
            _googleAuthService = googleAuthService;
            _appleAuthService = appleAuthService; 
            _userRepository = userRepository;
            _activityService = activityService;
            _foodIntakeService = foodIntakeService;
            _mapper = mapper;
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
        /// Google OAuth authentication with ID token ONLY
        /// </summary>
        /// <param name="request">Google authentication data with idToken</param>
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
                if (string.IsNullOrEmpty(request.IdToken))
                {
                    _logger.LogWarning("No idToken provided in Google auth request");
                    return BadRequest(new { error = "idToken is required" });
                }

                _logger.LogInformation($"Processing Google ID token authentication");
                _logger.LogInformation($"ID Token length: {request.IdToken.Length}");

                var result = await _googleAuthService.AuthenticateWithIdTokenAsync(request.IdToken);

                _logger.LogInformation($"Google authentication successful for user: {result.User.Email}");

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
                _logger.LogError($"Stack trace: {ex.StackTrace}");
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

        [HttpPost("apple")]
        [ProducesResponseType(typeof(AuthResponseDto), 200)]
        [ProducesResponseType(400)]
        [ProducesResponseType(401)]
        public async Task<IActionResult> AppleAuth([FromBody] AppleAuthRequest request)
        {
            try
            {
                if (string.IsNullOrEmpty(request.IdToken))
                {
                    _logger.LogWarning("No idToken provided in Apple auth request");
                    return BadRequest(new { error = "idToken is required" });
                }

                _logger.LogInformation($"🍎 Processing Apple ID token authentication");

                var result = await _appleAuthService.AuthenticateWithIdTokenAsync(request.IdToken, request.AuthorizationCode);

                _logger.LogInformation($"🍎 Apple authentication successful for user: {result.User.Email}");

                return Ok(result);
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogWarning($"🍎 Apple authentication failed: {ex.Message}");
                return Unauthorized(new { error = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError($"🍎 Apple authentication error: {ex.Message}");
                _logger.LogError($"Stack trace: {ex.StackTrace}");
                return BadRequest(new { error = ex.Message });
            }
        }
    }
}
using FitnessTracker.API.DTOs;
using FitnessTracker.API.Services;
using FitnessTracker.API.Repositories; 
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace FitnessTracker.API.Controllers
{
    [ApiController]
    [Route("api/user")]
    [Authorize]
    public class UserController : ControllerBase
    {
        private readonly IUserService _userService;
        private readonly IUserRepository _userRepository; 
        private readonly ILocalizationService _localizationService; 
        private readonly ILogger<UserController> _logger;

        public UserController(
            IUserService userService,
            IUserRepository userRepository, 
            ILocalizationService localizationService, 
            ILogger<UserController> logger)
        {
            _userService = userService;
            _userRepository = userRepository; 
            _localizationService = localizationService; 
            _logger = logger;
        }

        [HttpGet("profile")]
        public async Task<IActionResult> GetProfile()
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                var userIdClaim = User.FindFirst("user_id")?.Value;

                _logger.LogInformation($"GetProfile request - UserId: {userId}, UserIdClaim: {userIdClaim}");

                if (string.IsNullOrEmpty(userId))
                {
                    _logger.LogWarning("No user ID found in token claims");
                    return Unauthorized(new { error = "Invalid token - no user ID found" });
                }

                var user = await _userService.GetUserByIdAsync(userId);
                if (user == null)
                {
                    _logger.LogWarning($"User not found: {userId}");
                    return NotFound(new { error = "User not found", userId });
                }

                _logger.LogInformation($"Profile retrieved successfully for user {userId}");
                return Ok(user);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error getting profile: {ex.Message}");
                _logger.LogError($"Stack trace: {ex.StackTrace}");
                return BadRequest(new { error = ex.Message });
            }
        }

        [HttpPut("profile")]
        public async Task<IActionResult> UpdateProfile([FromBody] UpdateUserProfileRequest request)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                {
                    _logger.LogWarning("No user ID found in token for profile update");
                    return Unauthorized(new { error = "Invalid token" });
                }

                _logger.LogInformation($"Updating profile for user {userId}");

                var updatedUser = await _userService.UpdateUserProfileAsync(userId, request);

                _logger.LogInformation($"Profile updated successfully for user {userId}");
                return Ok(updatedUser);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error updating profile: {ex.Message}");
                return BadRequest(new { error = ex.Message });
            }
        }

        [HttpDelete("profile")]
        public async Task<IActionResult> DeleteProfile()
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized();

                await _userService.DeleteUserAsync(userId);
                return Ok(new { success = true });
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error deleting profile: {ex.Message}");
                return BadRequest(new { error = ex.Message });
            }
        }

        [HttpPost("locale")]
        public async Task<IActionResult> SetLocale([FromBody] SetLocaleRequest request)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized();

                if (string.IsNullOrEmpty(request.Locale))
                    return BadRequest(new { error = "Locale is required" });

                var language = _localizationService.GetLanguageFromLocale(request.Locale);
                var supportedLanguages = new[] { "ru", "en", "es", "de", "fr", "zh", "ja", "ko", "pt", "it" };

                if (!supportedLanguages.Contains(language))
                {
                    return BadRequest(new
                    {
                        error = "Unsupported language",
                        provided_locale = request.Locale,
                        extracted_language = language,
                        supported_languages = supportedLanguages,
                        examples = new
                        {
                            en = new[] { "en", "en_US", "en_GB", "en_ID", "en_AU", "en_CA" },
                            ru = new[] { "ru", "ru_RU", "ru_BY", "ru_KZ" },
                            es = new[] { "es", "es_ES", "es_MX", "es_AR" }
                        }
                    });
                }

                var user = await _userRepository.GetByIdAsync(userId);
                if (user == null)
                    return NotFound();

                user.Locale = request.Locale;
                await _userRepository.UpdateAsync(user);

                _logger.LogInformation($"🌍 Locale updated for user {userId}: '{request.Locale}' → language '{language}'");

                return Ok(new
                {
                    success = true,
                    original_locale = request.Locale,
                    extracted_language = language,
                    message = $"Language updated successfully to {language}",
                    examples_accepted = new
                    {
                        english = new[] { "en", "en_US", "en_GB", "en_ID", "en_AU" },
                        russian = new[] { "ru", "ru_RU", "ru_BY" }
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error setting locale: {ex.Message}");
                return BadRequest(new { error = ex.Message });
            }
        }

        /// <summary>
        /// 🌍 Получить доступные переводы для текущего языка пользователя
        /// </summary>
        /// <returns>Словарь переводов</returns>
        [HttpGet("translations")]
        public async Task<IActionResult> GetTranslations()
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized();

                var userLocale = await _localizationService.GetUserLocaleAsync(userId);
                var translations = _localizationService.GetAllTranslations(userLocale);

                return Ok(new
                {
                    locale = userLocale,
                    translations = translations
                });
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error getting translations: {ex.Message}");
                return BadRequest(new { error = ex.Message });
            }
        }

        [HttpPost("link-telegram")]
        public async Task<IActionResult> LinkTelegram([FromBody] LinkTelegramRequest request)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized();

                var user = await _userRepository.GetByIdAsync(userId);
                if (user == null)
                    return NotFound(new { error = "User not found" });

                var existingUser = await _userRepository.GetByTelegramIdAsync(request.TelegramId);
                if (existingUser != null && existingUser.Id != userId)
                {
                    _logger.LogWarning($"⚠️ Telegram ID {request.TelegramId} already linked to another user");
                    return BadRequest(new { error = "This Telegram account is already linked to another user" });
                }

                user.TelegramId = request.TelegramId;
                await _userRepository.UpdateAsync(user);

                _logger.LogInformation($"✅ Telegram ID {request.TelegramId} linked to user {user.Email}");

                return Ok(new
                {
                    success = true,
                    message = "Telegram account linked successfully",
                    telegramId = request.TelegramId
                });
            }
            catch (Exception ex)
            {
                _logger.LogError($"❌ Error linking Telegram: {ex.Message}");
                return StatusCode(500, new { error = "Internal server error" });
            }
        }

        public class LinkTelegramRequest
        {
            public long TelegramId { get; set; }
            public string Username { get; set; } = "";
        }

        public class SetLocaleRequest
        {
            public string Locale { get; set; } = string.Empty;
        }
    }
}
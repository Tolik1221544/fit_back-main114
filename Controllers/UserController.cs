using FitnessTracker.API.DTOs;
using FitnessTracker.API.Services;
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
        private readonly ILogger<UserController> _logger;

        public UserController(IUserService userService, ILogger<UserController> logger)
        {
            _userService = userService;
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
    }
}
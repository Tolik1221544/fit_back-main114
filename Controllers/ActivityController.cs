using FitnessTracker.API.DTOs;
using FitnessTracker.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace FitnessTracker.API.Controllers
{
    [ApiController]
    [Route("api/activity")]
    [Authorize]
    public class ActivityController : ControllerBase
    {
        private readonly IActivityService _activityService;
        private readonly IMissionService _missionService;

        public ActivityController(IActivityService activityService, IMissionService missionService)
        {
            _activityService = activityService;
            _missionService = missionService;
        }

        [HttpGet]
        public async Task<IActionResult> GetActivities([FromQuery] string? type = null, [FromQuery] DateTime? startDate = null, [FromQuery] DateTime? endDate = null)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized();

                var activities = await _activityService.GetUserActivitiesAsync(userId, type, startDate, endDate);
                return Ok(activities);
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        [HttpGet("{activityId}")]
        public async Task<IActionResult> GetActivity(string activityId)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized();

                var activity = await _activityService.GetActivityByIdAsync(userId, activityId);
                if (activity == null)
                    return NotFound();

                return Ok(activity);
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        [HttpPost]
        public async Task<IActionResult> AddActivity([FromBody] AddActivityRequest request)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized();

                var activity = await _activityService.AddActivityAsync(userId, request);

                // Update mission progress
                await _missionService.UpdateMissionProgressAsync(userId, "activity", 1);

                return Ok(activity);
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        [HttpPut("{activityId}")]
        public async Task<IActionResult> UpdateActivity(string activityId, [FromBody] UpdateActivityRequest request)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized();

                var updatedActivity = await _activityService.UpdateActivityAsync(userId, activityId, request);
                return Ok(updatedActivity);
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        [HttpDelete("{activityId}")]
        public async Task<IActionResult> DeleteActivity(string activityId)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized();

                await _activityService.DeleteActivityAsync(userId, activityId);
                return Ok(new { success = true });
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        [HttpGet("stats")]
        public async Task<IActionResult> GetActivityStats([FromQuery] DateTime? startDate = null, [FromQuery] DateTime? endDate = null)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized();

                var stats = await _activityService.GetActivityStatsAsync(userId, startDate, endDate);
                return Ok(stats);
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }
    }
}
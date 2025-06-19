using FitnessTracker.API.DTOs;
using FitnessTracker.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace FitnessTracker.API.Controllers
{
    [ApiController]
    [Route("api/food-intake")]
    [Authorize]
    public class FoodIntakeController : ControllerBase
    {
        private readonly IFoodIntakeService _foodIntakeService;
        private readonly IMissionService _missionService;

        public FoodIntakeController(IFoodIntakeService foodIntakeService, IMissionService missionService)
        {
            _foodIntakeService = foodIntakeService;
            _missionService = missionService;
        }

        [HttpGet]
        public async Task<IActionResult> GetFoodIntakes([FromQuery] DateTime? date = null)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized();

                IEnumerable<FoodIntakeDto> foodIntakes;
                if (date.HasValue)
                {
                    foodIntakes = await _foodIntakeService.GetUserFoodIntakesByDateAsync(userId, date.Value);
                }
                else
                {
                    foodIntakes = await _foodIntakeService.GetUserFoodIntakesAsync(userId);
                }

                return Ok(foodIntakes);
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        [HttpPost]
        public async Task<IActionResult> AddFoodIntake([FromBody] AddFoodIntakeRequest request)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized();

                var foodIntakes = await _foodIntakeService.AddFoodIntakeAsync(userId, request);
                
                // Update mission progress
                await _missionService.UpdateMissionProgressAsync(userId, "food_intake", request.Items.Count);

                return Ok(foodIntakes);
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        [HttpPut("{foodIntakeId}")]
        public async Task<IActionResult> UpdateFoodIntake(string foodIntakeId, [FromBody] UpdateFoodIntakeRequest request)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized();

                var updatedFoodIntake = await _foodIntakeService.UpdateFoodIntakeAsync(userId, foodIntakeId, request);
                return Ok(updatedFoodIntake);
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        [HttpDelete("{foodIntakeId}")]
        public async Task<IActionResult> DeleteFoodIntake(string foodIntakeId)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized();

                await _foodIntakeService.DeleteFoodIntakeAsync(userId, foodIntakeId);
                return Ok(new { success = true });
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        [HttpPost("scan")]
        public async Task<IActionResult> ScanFood(IFormFile image)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized();

                using var memoryStream = new MemoryStream();
                await image.CopyToAsync(memoryStream);
                var imageData = memoryStream.ToArray();

                var result = await _foodIntakeService.ScanFoodAsync(userId, imageData);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }
    }
}

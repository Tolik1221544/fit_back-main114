using FitnessTracker.API.DTOs;
using FitnessTracker.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;

namespace FitnessTracker.API.Controllers
{
    /// <summary>
    /// 🍎 Управление питанием и приемами пищи
    /// </summary>
    [ApiController]
    [Route("api/food-intake")]
    [Authorize]
    [Produces("application/json")]
    public class FoodIntakeController : ControllerBase
    {
        private readonly IFoodIntakeService _foodIntakeService;
        private readonly IMissionService _missionService;

        public FoodIntakeController(IFoodIntakeService foodIntakeService, IMissionService missionService)
        {
            _foodIntakeService = foodIntakeService;
            _missionService = missionService;
        }

        /// <summary>
        /// 📋 Получить записи питания за день или все записи
        /// </summary>
        /// <param name="date">Дата в формате YYYY-MM-DD (опционально). Если не указана - возвращает все записи</param>
        /// <returns>Список записей питания</returns>
        /// <response code="200">Записи питания успешно получены</response>
        /// <response code="401">Требуется авторизация</response>
        /// <example>
        /// GET /api/food-intake?date=2025-06-26
        /// </example>
        [HttpGet]
        [ProducesResponseType(typeof(IEnumerable<FoodIntakeDto>), 200)]
        [ProducesResponseType(401)]
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

        /// <summary>
        /// ➕ Добавить прием пищи с несколькими продуктами
        /// </summary>
        /// <param name="request">
        /// Данные приема пищи. Можно добавить несколько продуктов за раз.
        /// tempItemId - опциональный ID для связи с фронтендом
        /// weightType - "g" для граммов, "ml" для миллилитров
        /// image - опциональная ссылка на изображение продукта
        /// </param>
        /// <returns>Список добавленных записей</returns>
        /// <response code="200">Прием пищи успешно добавлен</response>
        /// <response code="400">Неверные данные запроса</response>
        /// <response code="401">Требуется авторизация</response>
        /// <example>
        /// {
        ///   "items": [
        ///     {
        ///       "tempItemId": "temp1",
        ///       "name": "Овсянка Геркулес",
        ///       "weight": 100,
        ///       "weightType": "g",
        ///       "image": "https://example.com/oats.jpg",
        ///       "nutritionPer100g": {
        ///         "calories": 389,
        ///         "proteins": 16.9,
        ///         "fats": 6.9,
        ///         "carbs": 66.3
        ///       }
        ///     },
        ///     {
        ///       "name": "Молоко 2.5%",
        ///       "weight": 200,
        ///       "weightType": "ml",
        ///       "nutritionPer100g": {
        ///         "calories": 52,
        ///         "proteins": 2.8,
        ///         "fats": 2.5,
        ///         "carbs": 4.7
        ///       }
        ///     }
        ///   ],
        ///   "dateTime": "2025-06-26T08:00:00Z"
        /// }
        /// </example>
        [HttpPost]
        [ProducesResponseType(typeof(IEnumerable<FoodIntakeDto>), 200)]
        [ProducesResponseType(400)]
        [ProducesResponseType(401)]
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

        /// <summary>
        /// ✏️ Обновить запись о приеме пищи
        /// </summary>
        /// <param name="foodIntakeId">ID записи питания</param>
        /// <param name="request">Обновленные данные</param>
        /// <returns>Обновленная запись</returns>
        [HttpPut("{foodIntakeId}")]
        [ProducesResponseType(typeof(FoodIntakeDto), 200)]
        [ProducesResponseType(400)]
        [ProducesResponseType(404)]
        [ProducesResponseType(401)]
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

        /// <summary>
        /// 🗑️ Удалить запись о приеме пищи
        /// </summary>
        /// <param name="foodIntakeId">ID записи питания</param>
        /// <returns>Результат удаления</returns>
        [HttpDelete("{foodIntakeId}")]
        [ProducesResponseType(200)]
        [ProducesResponseType(404)]
        [ProducesResponseType(401)]
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

        /// <summary>
        /// 📸 Сканировать продукт по фото (требует LW Coins)
        /// </summary>
        /// <param name="image">Изображение продукта</param>
        /// <returns>Распознанная информация о продукте</returns>
        /// <response code="200">Продукт успешно распознан</response>
        /// <response code="400">Недостаточно LW Coins или ошибка обработки</response>
        /// <response code="401">Требуется авторизация</response>
        /// <remarks>
        /// Эта функция тратит 1 LW Coin за каждое сканирование.
        /// Убедитесь что у пользователя достаточно монет или активна премиум подписка.
        /// </remarks>
        [HttpPost("scan")]
        [ProducesResponseType(typeof(ScanFoodResponse), 200)]
        [ProducesResponseType(400)]
        [ProducesResponseType(401)]
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
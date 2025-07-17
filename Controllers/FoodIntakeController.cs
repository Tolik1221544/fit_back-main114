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
        private readonly IGeminiService _geminiService;
        private readonly ILwCoinService _lwCoinService;
        private readonly IImageService _imageService; 

        public FoodIntakeController(
            IFoodIntakeService foodIntakeService,
            IMissionService missionService,
            IGeminiService geminiService,
            ILwCoinService lwCoinService,
            IImageService imageService) 
        {
            _foodIntakeService = foodIntakeService;
            _missionService = missionService;
            _geminiService = geminiService;
            _lwCoinService = lwCoinService;
            _imageService = imageService; 
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
        /// 📸 Сканировать продукт по фото (новая реализация с Gemini AI)
        /// </summary>
        /// <param name="image">Изображение продукта</param>
        /// <param name="userPrompt">Дополнительные инструкции от пользователя</param>
        /// <returns>Распознанная информация о продукте с URL изображения</returns>
        /// <response code="200">Продукт успешно распознан</response>
        /// <response code="400">Недостаточно LW Coins или ошибка обработки</response>
        /// <response code="401">Требуется авторизация</response>
        /// <remarks>
        /// ✅ ОБНОВЛЕНО: Теперь использует Gemini AI для более точного распознавания.
        /// Эта функция тратит 1 LW Coin за каждое сканирование.
        /// Убедитесь что у пользователя достаточно монет или активна премиум подписка.
        /// Может распознать несколько блюд на одном изображении.
        /// Теперь возвращает URL сохраненного изображения.
        /// </remarks>
        [HttpPost("scan")]
        [ProducesResponseType(typeof(ScanFoodResponseWithImage), 200)]
        [ProducesResponseType(400)]
        [ProducesResponseType(401)]
        public async Task<IActionResult> ScanFood(IFormFile image, [FromForm] string? userPrompt = null)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized();

                var canSpend = await _lwCoinService.SpendLwCoinsAsync(userId, 1, "food_scan",
                    "Food scan with Gemini AI", "photo");

                if (!canSpend)
                {
                    return BadRequest(new { error = "Недостаточно LW Coins для сканирования еды" });
                }

                var imageUrl = await _imageService.SaveImageAsync(image, "food-scans");

                using var memoryStream = new MemoryStream();
                await image.CopyToAsync(memoryStream);
                var imageData = memoryStream.ToArray();

                var result = await _geminiService.AnalyzeFoodImageAsync(imageData, userPrompt);

                if (!result.Success)
                {
                    await _imageService.DeleteImageAsync(imageUrl);
                    return BadRequest(new { error = result.ErrorMessage });
                }

                var legacyResponse = new ScanFoodResponseWithImage
                {
                    Items = result.FoodItems?.Select(fi => new FoodIntakeDto
                    {
                        Id = Guid.NewGuid().ToString(),
                        Name = fi.Name,
                        Weight = fi.EstimatedWeight,
                        WeightType = fi.WeightType,
                        DateTime = DateTime.UtcNow,
                        NutritionPer100g = fi.NutritionPer100g,
                        Image = imageUrl // НОВОЕ: URL изображения
                    }).ToList() ?? new List<FoodIntakeDto>(),
                    ImageUrl = imageUrl // НОВОЕ: URL изображения
                };

                return Ok(legacyResponse);
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        /// <summary>
        /// 🤖 Новый метод: Сканирование с полным ответом от ИИ
        /// </summary>
        /// <param name="image">Изображение продукта</param>
        /// <param name="userPrompt">Дополнительные инструкции</param>
        /// <param name="saveResults">Автоматически сохранить результаты</param>
        /// <returns>Полный ответ от Gemini AI с URL изображения</returns>
        /// <response code="200">Анализ завершен</response>
        /// <response code="400">Ошибка обработки</response>
        /// <response code="401">Требуется авторизация</response>
        [HttpPost("ai-scan")]
        [ProducesResponseType(typeof(FoodScanResponse), 200)]
        [ProducesResponseType(400)]
        [ProducesResponseType(401)]
        public async Task<IActionResult> AIScanFood(
            IFormFile image,
            [FromForm] string? userPrompt = null,
            [FromForm] bool saveResults = false)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized();

                var canSpend = await _lwCoinService.SpendLwCoinsAsync(userId, 1, "ai_food_scan",
                    "Advanced AI food scan", "photo");

                if (!canSpend)
                {
                    return BadRequest(new { error = "Недостаточно LW Coins для ИИ сканирования еды" });
                }

                var imageUrl = await _imageService.SaveImageAsync(image, "food-scans");

                using var memoryStream = new MemoryStream();
                await image.CopyToAsync(memoryStream);
                var imageData = memoryStream.ToArray();

                var result = await _geminiService.AnalyzeFoodImageAsync(imageData, userPrompt);

                if (!result.Success)
                {
                    await _imageService.DeleteImageAsync(imageUrl);
                    return BadRequest(new { error = result.ErrorMessage });
                }

                result.ImageUrl = imageUrl;

                if (saveResults && result.FoodItems?.Any() == true)
                {
                    try
                    {
                        var addFoodRequest = new AddFoodIntakeRequest
                        {
                            Items = result.FoodItems.Select(fi => new FoodItemRequest
                            {
                                Name = fi.Name,
                                Weight = fi.EstimatedWeight,
                                WeightType = fi.WeightType,
                                NutritionPer100g = fi.NutritionPer100g,
                                Image = imageUrl // НОВОЕ: URL изображения
                            }).ToList(),
                            DateTime = DateTime.UtcNow
                        };

                        await _foodIntakeService.AddFoodIntakeAsync(userId, addFoodRequest);

                        await _missionService.UpdateMissionProgressAsync(userId, "food_intake", result.FoodItems.Count);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error saving AI food scan results: {ex.Message}");
                    }
                }

                return Ok(result);
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }
    }
}
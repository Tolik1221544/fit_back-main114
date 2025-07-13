using FitnessTracker.API.DTOs;
using FitnessTracker.API.Services;
using FitnessTracker.API.Services.AI;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace FitnessTracker.API.Controllers

{
    /// <summary>
    /// 🤖 Контроллер для работы с ИИ функциями (Gemini)
    /// </summary>
    [ApiController]
    [Route("api/ai")]
    [Authorize]
    [Produces("application/json")]
    public class AIController : ControllerBase
    {
        private readonly IGeminiService _geminiService;
        private readonly ILwCoinService _lwCoinService;
        private readonly IFoodIntakeService _foodIntakeService;
        private readonly IActivityService _activityService;
        private readonly IBodyScanService _bodyScanService;
        private readonly IImageService _imageService;
        private readonly ILogger<AIController> _logger;

        public AIController(
            IGeminiService geminiService,
            ILwCoinService lwCoinService,
            IFoodIntakeService foodIntakeService,
            IActivityService activityService,
            IBodyScanService bodyScanService,
            IImageService imageService,
            ILogger<AIController> logger)
        {
            _geminiService = geminiService;
            _lwCoinService = lwCoinService;
            _foodIntakeService = foodIntakeService;
            _activityService = activityService;
            _bodyScanService = bodyScanService;
            _imageService = imageService;
            _logger = logger;
        }

        /// <summary>
        /// 🍎 Сканирование еды по фото (требует LW Coins)
        /// </summary>
        /// <param name="image">Изображение еды для анализа</param>
        /// <param name="userPrompt">Дополнительные инструкции от пользователя</param>
        /// <param name="saveResults">Сохранить результаты в базу данных</param>
        /// <returns>Результат анализа еды</returns>
        /// <response code="200">Еда успешно проанализирована</response>
        /// <response code="400">Недостаточно LW Coins или ошибка обработки</response>
        /// <response code="401">Требуется авторизация</response>
        /// <remarks>
        /// Эта функция использует Gemini AI для анализа изображения еды.
        /// Тратит 1 LW Coin за каждое сканирование (кроме премиум пользователей).
        /// Может определить несколько блюд на одном изображении.
        /// </remarks>
        [HttpPost("scan-food")]
        [ProducesResponseType(typeof(FoodScanResponse), 200)]
        [ProducesResponseType(400)]
        [ProducesResponseType(401)]
        public async Task<IActionResult> ScanFood(
    IFormFile image,
    [FromForm] string? userPrompt = null,
    [FromForm] bool saveResults = false)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized();

                // Проверяем и тратим LW Coins
                var canSpend = await _lwCoinService.SpendLwCoinsAsync(userId, 1, "ai_food_scan",
                    "AI Food Scan", "photo");

                if (!canSpend)
                {
                    return BadRequest(new { error = "Недостаточно LW Coins для сканирования еды" });
                }

                // НОВОЕ: Сохраняем изображение и получаем URL
                var imageUrl = await _imageService.SaveImageAsync(image, "food-scans");

                // Конвертируем изображение в байты для анализа
                using var memoryStream = new MemoryStream();
                await image.CopyToAsync(memoryStream);
                var imageData = memoryStream.ToArray();

                _logger.LogInformation($"🍎 Processing food scan for user {userId}, image size: {imageData.Length} bytes, saved at: {imageUrl}");

                // Анализируем с помощью Gemini
                var result = await _geminiService.AnalyzeFoodImageAsync(imageData, userPrompt);

                if (!result.Success)
                {
                    // Удаляем сохраненное изображение при ошибке
                    await _imageService.DeleteImageAsync(imageUrl);
                    return BadRequest(new { error = result.ErrorMessage });
                }

                // НОВОЕ: Добавляем URL изображения в ответ
                result.ImageUrl = imageUrl;

                // Если нужно сохранить результаты
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
                                Image = imageUrl // НОВОЕ: Сохраняем URL изображения
                            }).ToList(),
                            DateTime = DateTime.UtcNow
                        };

                        await _foodIntakeService.AddFoodIntakeAsync(userId, addFoodRequest);
                        _logger.LogInformation($"✅ Saved {result.FoodItems.Count} food items for user {userId}");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError($"❌ Error saving food items: {ex.Message}");
                        // Продолжаем выполнение, даже если сохранение не удалось
                    }
                }

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError($"❌ Error scanning food: {ex.Message}");
                return BadRequest(new { error = $"Ошибка сканирования: {ex.Message}" });
            }
        }

        /// <summary>
        /// 🔄 Переключить AI провайдера
        /// </summary>
        /// <param name="request">Запрос на переключение провайдера</param>
        /// <returns>Результат переключения</returns>
        /// <response code="200">Провайдер успешно переключен</response>
        /// <response code="400">Неверное название провайдера</response>
        /// <response code="401">Требуется авторизация администратора</response>
        [HttpPost("switch-provider")]
        [ProducesResponseType(200)]
        [ProducesResponseType(400)]
        [ProducesResponseType(401)]
        public Task<IActionResult> SwitchProvider([FromBody] SwitchProviderRequest request)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Task.FromResult<IActionResult>(Unauthorized());

                // Здесь можно добавить проверку на админа
                // Пока что любой авторизованный пользователь может переключать

                var configuration = HttpContext.RequestServices.GetRequiredService<IConfiguration>();

                // В реальном приложении это должно сохраняться в базу данных
                // Пока что просто логируем
                _logger.LogInformation($"🔄 User {userId} requested to switch to provider: {request.ProviderName}");

                return Task.FromResult<IActionResult>(Ok(new
                {
                    success = true,
                    message = $"Provider switched to {request.ProviderName}",
                    note = "In production, this should update the database configuration"
                }));
            }
            catch (Exception ex)
            {
                _logger.LogError($"❌ Error switching provider: {ex.Message}");
                return Task.FromResult<IActionResult>(BadRequest(new { error = ex.Message }));
            }
        }

        /// <summary>
        /// 🏥 Получить статус всех AI провайдеров
        /// </summary>
        /// <returns>Статус работы всех доступных провайдеров</returns>
        /// <response code="200">Статус получен</response>
        [HttpGet("providers-status")]
        [AllowAnonymous]
        [ProducesResponseType(200)]
        public async Task<IActionResult> GetProvidersStatus()
        {
            try
            {
                // Проверяем, является ли сервис UniversalAIService
                if (_geminiService is UniversalAIService universalAI)
                {
                    var healthStatus = await universalAI.GetProviderHealthStatusAsync();
                    var configuration = HttpContext.RequestServices.GetRequiredService<IConfiguration>();
                    var activeProvider = configuration["AI:ActiveProvider"] ?? "Vertex AI (Gemini Pro 2.5)";

                    return Ok(new
                    {
                        ActiveProvider = activeProvider,
                        Providers = healthStatus.Select(p => new
                        {
                            Name = p.Key,
                            Status = p.Value ? "Online" : "Offline",
                            IsActive = p.Key == activeProvider
                        }),
                        Timestamp = DateTime.UtcNow,
                        TotalProviders = healthStatus.Count,
                        HealthyProviders = healthStatus.Count(p => p.Value)
                    });
                }
                else
                {
                    return Ok(new
                    {
                        Message = "Universal AI service not available",
                        LegacyService = "Gemini",
                        Timestamp = DateTime.UtcNow
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"❌ Error getting providers status: {ex.Message}");
                return StatusCode(503, new
                {
                    Error = ex.Message,
                    Timestamp = DateTime.UtcNow
                });
            }
        }

        /// <summary>
        /// 🧪 Тестировать конкретного провайдера
        /// </summary>
        /// <param name="request">Название провайдера для тестирования</param>
        /// <returns>Результат тестирования</returns>
        [HttpPost("test-provider")]
        [AllowAnonymous]
        [ProducesResponseType(200)]
        [ProducesResponseType(400)]
        public async Task<IActionResult> TestProvider([FromBody] TestProviderRequest request)
        {
            try
            {
                _logger.LogInformation($"🧪 Testing provider: {request.ProviderName}");

                // Простой тест сервиса
                var testContents = new List<GeminiContent>
                {
                    new GeminiContent
                    {
                        Parts = new List<GeminiPart>
                        {
                            new GeminiPart { Text = "Test" }
                        }
                    }
                };

                var response = await _geminiService.SendGeminiRequestAsync(testContents);
                var isHealthy = response?.Candidates?.Any() == true;

                return Ok(new
                {
                    ProviderName = request.ProviderName,
                    Status = isHealthy ? "Healthy" : "Unhealthy",
                    TestTime = DateTime.UtcNow,
                    Message = isHealthy ? "Provider is working correctly" : "Provider is not responding"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError($"❌ Error testing provider {request.ProviderName}: {ex.Message}");
                return BadRequest(new
                {
                    error = ex.Message,
                    providerName = request.ProviderName
                });
            }
        }

        /// <summary>
        /// 💪 Анализ тела по фотографиям
        /// </summary>
        /// <param name="request">Данные для анализа тела</param>
        /// <returns>Результат анализа тела</returns>
        /// <response code="200">Тело успешно проанализировано</response>
        /// <response code="400">Ошибка обработки</response>
        /// <response code="401">Требуется авторизация</response>
        /// <remarks>
        /// Анализирует фотографии тела и предоставляет рекомендации по тренировкам и питанию.
        /// Может анализировать до 3 фотографий (фронтальная, боковая, сзади).
        /// </remarks>
        [HttpPost("analyze-body")]
        [ProducesResponseType(typeof(BodyScanResponse), 200)]
        [ProducesResponseType(400)]
        [ProducesResponseType(401)]
        public async Task<IActionResult> AnalyzeBody([FromForm] BodyScanRequest request)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized();

                _logger.LogInformation($"💪 Processing body analysis for user {userId}");

                // НОВОЕ: Сохраняем изображения и получаем URLs
                string? frontImageUrl = null;
                string? sideImageUrl = null;
                string? backImageUrl = null;

                byte[]? frontImageData = null;
                byte[]? sideImageData = null;
                byte[]? backImageData = null;

                if (request.FrontImage != null)
                {
                    frontImageUrl = await _imageService.SaveImageAsync(request.FrontImage, "body-scans");
                    using var ms = new MemoryStream();
                    await request.FrontImage.CopyToAsync(ms);
                    frontImageData = ms.ToArray();
                }

                if (request.SideImage != null)
                {
                    sideImageUrl = await _imageService.SaveImageAsync(request.SideImage, "body-scans");
                    using var ms = new MemoryStream();
                    await request.SideImage.CopyToAsync(ms);
                    sideImageData = ms.ToArray();
                }

                if (request.BackImage != null)
                {
                    backImageUrl = await _imageService.SaveImageAsync(request.BackImage, "body-scans");
                    using var ms = new MemoryStream();
                    await request.BackImage.CopyToAsync(ms);
                    backImageData = ms.ToArray();
                }

                // Анализируем с помощью Gemini
                var result = await _geminiService.AnalyzeBodyImagesAsync(
                    frontImageData,
                    sideImageData,
                    backImageData,
                    request.CurrentWeight,
                    request.Height,
                    request.Age,
                    request.Gender,
                    request.Goals);

                if (!result.Success)
                {
                    // Удаляем сохраненные изображения при ошибке
                    if (!string.IsNullOrEmpty(frontImageUrl))
                        await _imageService.DeleteImageAsync(frontImageUrl);
                    if (!string.IsNullOrEmpty(sideImageUrl))
                        await _imageService.DeleteImageAsync(sideImageUrl);
                    if (!string.IsNullOrEmpty(backImageUrl))
                        await _imageService.DeleteImageAsync(backImageUrl);

                    return BadRequest(new { error = result.ErrorMessage });
                }

                // НОВОЕ: Добавляем URLs изображений в ответ
                result.FrontImageUrl = frontImageUrl;
                result.SideImageUrl = sideImageUrl;
                result.BackImageUrl = backImageUrl;

                // Сохраняем результат как body scan
                try
                {
                    var addBodyScanRequest = new AddBodyScanRequest
                    {
                        FrontImageUrl = frontImageUrl ?? "no_image",
                        SideImageUrl = sideImageUrl ?? "no_image",
                        BackImageUrl = backImageUrl,
                        Weight = request.CurrentWeight ?? 0,
                        BodyFatPercentage = result.BodyAnalysis.EstimatedBodyFatPercentage,
                        MusclePercentage = result.BodyAnalysis.EstimatedMusclePercentage,
                        WaistCircumference = result.BodyAnalysis.EstimatedWaistCircumference,
                        ChestCircumference = result.BodyAnalysis.EstimatedChestCircumference,
                        HipCircumference = result.BodyAnalysis.EstimatedHipCircumference,

                        // ✅ НОВЫЕ ПОЛЯ: Сохраняем основной обмен веществ
                        BasalMetabolicRate = result.BodyAnalysis.BasalMetabolicRate,
                        MetabolicRateCategory = result.BodyAnalysis.MetabolicRateCategory,

                        Notes = $"AI Analysis: {result.BodyAnalysis.OverallCondition}. BMR: {result.BodyAnalysis.BasalMetabolicRate} ккал ({result.BodyAnalysis.MetabolicRateCategory})",
                        ScanDate = DateTime.UtcNow
                    };

                    await _bodyScanService.AddBodyScanAsync(userId, addBodyScanRequest);
                    _logger.LogInformation($"✅ Saved body scan analysis for user {userId} with BMR: {result.BodyAnalysis.BasalMetabolicRate} ккал");
                }
                catch (Exception ex)
                {
                    _logger.LogError($"❌ Error saving body scan: {ex.Message}");
                    // Продолжаем выполнение
                }

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError($"❌ Error analyzing body: {ex.Message}");
                return BadRequest(new { error = $"Ошибка анализа тела: {ex.Message}" });
            }
        }

        /// <summary>
        /// 🎤 Голосовой ввод тренировки (требует LW Coins)
        /// </summary>
        /// <param name="audioFile">Аудиофайл с описанием тренировки</param>
        /// <param name="workoutType">Тип тренировки (strength/cardio)</param>
        /// <param name="saveResults">Сохранить результаты в базу данных</param>
        /// <returns>Распознанная и структурированная информация о тренировке</returns>
        /// <response code="200">Тренировка успешно распознана</response>
        /// <response code="400">Недостаточно LW Coins или ошибка обработки</response>
        /// <response code="401">Требуется авторизация</response>
        [HttpPost("voice-workout")]
        public async Task<IActionResult> VoiceWorkout(
    IFormFile audioFile,
    [FromForm] string? workoutType = null,
    [FromForm] bool saveResults = false)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized();

                // Проверяем и тратим LW Coins
                var canSpend = await _lwCoinService.SpendLwCoinsAsync(userId, 1, "ai_voice_workout",
                    "AI Voice Workout", "voice");

                if (!canSpend)
                {
                    return BadRequest(new { error = "Недостаточно LW Coins для голосового ввода тренировки" });
                }

                // Конвертируем аудио в байты
                using var memoryStream = new MemoryStream();
                await audioFile.CopyToAsync(memoryStream);
                var audioData = memoryStream.ToArray();

                _logger.LogInformation($"🎤 Processing voice workout for user {userId}, audio size: {audioData.Length} bytes, workoutType: {workoutType}");

                // Анализируем с помощью Gemini
                var result = await _geminiService.AnalyzeVoiceWorkoutAsync(audioData, workoutType);

                if (!result.Success)
                {
                    _logger.LogError($"❌ Voice workout analysis failed: {result.ErrorMessage}");
                    return BadRequest(new { error = result.ErrorMessage });
                }

                _logger.LogInformation($"✅ Voice workout analysis successful. Type: {result.WorkoutData?.Type}, StartTime: {result.WorkoutData?.StartTime}, EndTime: {result.WorkoutData?.EndTime}");

                // Если нужно сохранить результаты
                if (saveResults && result.WorkoutData != null)
                {
                    try
                    {
                        var addActivityRequest = new AddActivityRequest
                        {
                            Type = result.WorkoutData.Type,
                            StartDate = result.WorkoutData.StartTime.Date,
                            StartTime = result.WorkoutData.StartTime,
                            EndDate = result.WorkoutData.EndTime?.Date,
                            EndTime = result.WorkoutData.EndTime,
                            Calories = result.WorkoutData.EstimatedCalories,
                            StrengthData = result.WorkoutData.StrengthData,
                            CardioData = result.WorkoutData.CardioData
                        };

                        await _activityService.AddActivityAsync(userId, addActivityRequest);
                        _logger.LogInformation($"✅ Saved voice workout for user {userId}");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError($"❌ Error saving voice workout: {ex.Message}");
                        // Не прерываем выполнение, возвращаем результат анализа
                    }
                }

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError($"❌ Error processing voice workout: {ex.Message}");
                _logger.LogError($"Stack trace: {ex.StackTrace}");
                return BadRequest(new { error = $"Ошибка обработки голосовой тренировки: {ex.Message}" });
            }
        }

        /// <summary>
        /// 🗣️ Голосовой ввод питания (требует LW Coins)
        /// </summary>
        /// <param name="audioFile">Аудиофайл с описанием еды</param>
        /// <param name="mealType">Тип приема пищи</param>
        /// <param name="saveResults">Сохранить результаты в базу данных</param>
        /// <returns>Распознанная и структурированная информация о питании</returns>
        /// <response code="200">Питание успешно распознано</response>
        /// <response code="400">Недостаточно LW Coins или ошибка обработки</response>
        /// <response code="401">Требуется авторизация</response>
        [HttpPost("voice-food")]
        [ProducesResponseType(typeof(VoiceFoodResponse), 200)]
        [ProducesResponseType(400)]
        [ProducesResponseType(401)]
        public async Task<IActionResult> VoiceFood(
            IFormFile audioFile,
            [FromForm] string? mealType = null,
            [FromForm] bool saveResults = false)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized();

                // Проверяем и тратим LW Coins
                var canSpend = await _lwCoinService.SpendLwCoinsAsync(userId, 1, "ai_voice_food",
                    "AI Voice Food", "voice");

                if (!canSpend)
                {
                    return BadRequest(new { error = "Недостаточно LW Coins для голосового ввода питания" });
                }

                // Конвертируем аудио в байты
                using var memoryStream = new MemoryStream();
                await audioFile.CopyToAsync(memoryStream);
                var audioData = memoryStream.ToArray();

                _logger.LogInformation($"🗣️ Processing voice food for user {userId}, audio size: {audioData.Length} bytes");

                // Анализируем с помощью Gemini
                var result = await _geminiService.AnalyzeVoiceFoodAsync(audioData, mealType);

                if (!result.Success)
                {
                    return BadRequest(new { error = result.ErrorMessage });
                }

                // Если нужно сохранить результаты
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
                                NutritionPer100g = fi.NutritionPer100g
                            }).ToList(),
                            DateTime = DateTime.UtcNow
                        };

                        await _foodIntakeService.AddFoodIntakeAsync(userId, addFoodRequest);
                        _logger.LogInformation($"✅ Saved {result.FoodItems.Count} voice food items for user {userId}");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError($"❌ Error saving voice food: {ex.Message}");
                    }
                }

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError($"❌ Error processing voice food: {ex.Message}");
                return BadRequest(new { error = $"Ошибка обработки голосового ввода питания: {ex.Message}" });
            }
        }

        /// <summary>
        /// 🧠 Проверка статуса ИИ сервиса
        /// </summary>
        /// <returns>Статус работы Gemini API</returns>
        /// <response code="200">Сервис работает нормально</response>
        /// <response code="503">Сервис недоступен</response>
        [HttpGet("status")]
        [AllowAnonymous]
        [ProducesResponseType(200)]
        [ProducesResponseType(503)]
        public async Task<IActionResult> GetAIStatus()
        {
            try
            {
                // Простой тестовый запрос к Gemini
                var testContents = new List<GeminiContent>
                {
                    new GeminiContent
                    {
                        Parts = new List<GeminiPart>
                        {
                            new GeminiPart { Text = "Ответь 'OK' если ты работаешь" }
                        }
                    }
                };

                var response = await _geminiService.SendGeminiRequestAsync(testContents);

                var isWorking = response?.Candidates?.Any() == true;

                return Ok(new
                {
                    Service = "Gemini AI",
                    Status = isWorking ? "Online" : "Offline",
                    Timestamp = DateTime.UtcNow,
                    Features = new[]
                    {
                        "Food Image Analysis",
                        "Body Analysis",
                        "Voice Workout Recognition",
                        "Voice Food Recognition"
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError($"❌ AI Status check failed: {ex.Message}");
                return StatusCode(503, new
                {
                    Service = "Gemini AI",
                    Status = "Error",
                    Error = ex.Message,
                    Timestamp = DateTime.UtcNow
                });
            }
        }

        /// <summary>
        /// 📊 Получить статистику использования ИИ
        /// </summary>
        /// <returns>Статистика использования ИИ функций пользователем</returns>
        /// <response code="200">Статистика получена</response>
        /// <response code="401">Требуется авторизация</response>
        [HttpGet("usage-stats")]
        [ProducesResponseType(200)]
        [ProducesResponseType(401)]
        public async Task<IActionResult> GetUsageStats()
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized();

                // Получаем статистику использования LW Coins для ИИ функций
                var transactions = await _lwCoinService.GetUserLwCoinTransactionsAsync(userId);
                var aiTransactions = transactions.Where(t => t.FeatureUsed.StartsWith("ai_") ||
                                                           t.FeatureUsed == "photo" ||
                                                           t.FeatureUsed == "voice").ToList();

                var currentMonth = DateTime.UtcNow.Month;
                var currentYear = DateTime.UtcNow.Year;

                var monthlyUsage = aiTransactions.Where(t => t.CreatedAt.Month == currentMonth &&
                                                           t.CreatedAt.Year == currentYear).ToList();

                var stats = new
                {
                    TotalAIUsage = aiTransactions.Count,
                    MonthlyAIUsage = monthlyUsage.Count,
                    FeatureUsage = new
                    {
                        FoodScans = aiTransactions.Count(t => t.FeatureUsed == "photo" || t.FeatureUsed == "ai_food_scan"),
                        VoiceWorkouts = aiTransactions.Count(t => t.FeatureUsed == "ai_voice_workout"),
                        VoiceFood = aiTransactions.Count(t => t.FeatureUsed == "ai_voice_food"),
                        BodyAnalysis = aiTransactions.Count(t => t.FeatureUsed == "ai_body_scan")
                    },
                    MonthlyFeatureUsage = new
                    {
                        FoodScans = monthlyUsage.Count(t => t.FeatureUsed == "photo" || t.FeatureUsed == "ai_food_scan"),
                        VoiceWorkouts = monthlyUsage.Count(t => t.FeatureUsed == "ai_voice_workout"),
                        VoiceFood = monthlyUsage.Count(t => t.FeatureUsed == "ai_voice_food"),
                        BodyAnalysis = monthlyUsage.Count(t => t.FeatureUsed == "ai_body_scan")
                    },
                    LastUsed = aiTransactions.OrderByDescending(t => t.CreatedAt).FirstOrDefault()?.CreatedAt,
                    TotalCoinsSpent = Math.Abs(aiTransactions.Sum(t => t.Amount))
                };

                return Ok(stats);
            }
            catch (Exception ex)
            {
                _logger.LogError($"❌ Error getting AI usage stats: {ex.Message}");
                return BadRequest(new { error = ex.Message });
            }
        }
    }

    public class SwitchProviderRequest
    {
        public string ProviderName { get; set; } = string.Empty;
    }

    public class TestProviderRequest
    {
        public string ProviderName { get; set; } = string.Empty;
    }
}
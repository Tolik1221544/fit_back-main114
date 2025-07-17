using FitnessTracker.API.DTOs;
using FitnessTracker.API.Services;
using FitnessTracker.API.Services.AI;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace FitnessTracker.API.Controllers
{
    /// <summary>
    /// 🤖 Контроллер для работы с ИИ функциями (Gemini) с сохранением аудио файлов
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
        private readonly IVoiceFileService _voiceFileService; 
        private readonly ILogger<AIController> _logger;

        public AIController(
            IGeminiService geminiService,
            ILwCoinService lwCoinService,
            IFoodIntakeService foodIntakeService,
            IActivityService activityService,
            IBodyScanService bodyScanService,
            IImageService imageService,
            IVoiceFileService voiceFileService, 
            ILogger<AIController> logger)
        {
            _geminiService = geminiService;
            _lwCoinService = lwCoinService;
            _foodIntakeService = foodIntakeService;
            _activityService = activityService;
            _bodyScanService = bodyScanService;
            _imageService = imageService;
            _voiceFileService = voiceFileService; 
            _logger = logger;
        }

        /// <summary>
        /// 🎤 Голосовой ввод тренировки (требует LW Coins) + сохранение аудио
        /// </summary>
        [HttpPost("voice-workout")]
        [ProducesResponseType(typeof(VoiceWorkoutResponseWithAudio), 200)]
        [ProducesResponseType(400)]
        [ProducesResponseType(401)]
        public async Task<IActionResult> VoiceWorkout(
            IFormFile audioFile,
            [FromForm] string? workoutType = null,
            [FromForm] bool saveResults = false,
            [FromForm] bool saveAudio = true)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized();

                if (audioFile == null || audioFile.Length == 0)
                {
                    return BadRequest(new { error = "Аудио файл не предоставлен" });
                }

                if (audioFile.Length > 50 * 1024 * 1024) // 50MB
                {
                    return BadRequest(new { error = "Размер аудио файла не должен превышать 50MB" });
                }

                var canSpend = await _lwCoinService.SpendLwCoinsAsync(userId, 1, "ai_voice_workout",
                    "AI Voice Workout", "voice");

                if (!canSpend)
                {
                    return BadRequest(new { error = "Недостаточно LW Coins для голосового ввода тренировки" });
                }

                string? audioFileId = null;
                string? audioUrl = null;
                double audioSizeMB = Math.Round(audioFile.Length / (1024.0 * 1024.0), 2);

                if (saveAudio)
                {
                    try
                    {
                        audioFileId = await _voiceFileService.SaveVoiceFileAsync(audioFile, userId, "workout");
                        audioUrl = _voiceFileService.GetDownloadUrl(audioFileId);
                        _logger.LogInformation($"🎤 Audio saved: {audioFileId} (Size: {audioSizeMB} MB)");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError($"❌ Failed to save audio: {ex.Message}");
                    }
                }

                using var memoryStream = new MemoryStream();
                await audioFile.CopyToAsync(memoryStream);
                var audioData = memoryStream.ToArray();

                _logger.LogInformation($"🎤 Processing voice workout for user {userId}, audio size: {audioData.Length} bytes, workoutType: {workoutType}");

                VoiceWorkoutResponse result;
                try
                {
                    result = await _geminiService.AnalyzeVoiceWorkoutAsync(audioData, workoutType);
                }
                catch (Exception ex)
                {
                    _logger.LogError($"❌ Voice workout analysis failed: {ex.Message}");

                    if (!string.IsNullOrEmpty(audioFileId))
                    {
                        try
                        {
                            await _voiceFileService.DeleteVoiceFileAsync(userId, audioFileId);
                        }
                        catch (Exception deleteEx)
                        {
                            _logger.LogError($"❌ Failed to delete audio file after error: {deleteEx.Message}");
                        }
                    }

                    return BadRequest(new { error = "Не удалось обработать аудио. Попробуйте еще раз или проверьте качество записи." });
                }

                if (!result.Success)
                {
                    _logger.LogError($"❌ Voice workout analysis failed: {result.ErrorMessage}");

                    if (!string.IsNullOrEmpty(audioFileId))
                    {
                        try
                        {
                            await _voiceFileService.DeleteVoiceFileAsync(userId, audioFileId);
                        }
                        catch (Exception deleteEx)
                        {
                            _logger.LogError($"❌ Failed to delete audio file after analysis failure: {deleteEx.Message}");
                        }
                    }

                    return BadRequest(new
                    {
                        error = result.ErrorMessage ?? "Не удалось распознать тренировку в аудио записи",
                        suggestion = "Попробуйте говорить четче или запишите аудио заново"
                    });
                }

                var response = new VoiceWorkoutResponseWithAudio
                {
                    Success = result.Success,
                    TranscribedText = result.TranscribedText,
                    WorkoutData = result.WorkoutData,
                    AudioUrl = audioUrl,
                    AudioFileId = audioFileId,
                    AudioSaved = !string.IsNullOrEmpty(audioFileId),
                    AudioSizeMB = audioSizeMB
                };

                _logger.LogInformation($"✅ Voice workout analysis successful. Type: {result.WorkoutData?.Type}, AudioSaved: {!string.IsNullOrEmpty(audioFileId)}");

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
                        _logger.LogInformation($"✅ Saved voice workout to database for user {userId}");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError($"❌ Error saving voice workout to database: {ex.Message}");
                    }
                }

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError($"❌ Unexpected error processing voice workout: {ex.Message}");
                _logger.LogError($"Stack trace: {ex.StackTrace}");

                return BadRequest(new
                {
                    error = "Произошла системная ошибка при обработке голосовой тренировки",
                    message = "Попробуйте еще раз через несколько минут"
                });
            }
        }

        /// <summary>
        /// 🗣️ Голосовой ввод питания (требует LW Coins) + сохранение аудио
        /// </summary>
        /// <param name="audioFile">Аудиофайл с описанием еды</param>
        /// <param name="mealType">Тип приема пищи</param>
        /// <param name="saveResults">Сохранить результаты в базу данных</param>
        /// <param name="saveAudio">Сохранить аудио файл на сервере</param>
        /// <returns>Распознанная и структурированная информация о питании + URL аудио</returns>
        /// <response code="200">Питание успешно распознано</response>
        /// <response code="400">Недостаточно LW Coins или ошибка обработки</response>
        /// <response code="401">Требуется авторизация</response>
        [HttpPost("voice-food")]
        [ProducesResponseType(typeof(VoiceFoodResponseWithAudio), 200)]
        [ProducesResponseType(400)]
        [ProducesResponseType(401)]
        public async Task<IActionResult> VoiceFood(
            IFormFile audioFile,
            [FromForm] string? mealType = null,
            [FromForm] bool saveResults = false,
            [FromForm] bool saveAudio = true) 
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized();

                var canSpend = await _lwCoinService.SpendLwCoinsAsync(userId, 1, "ai_voice_food",
                    "AI Voice Food", "voice");

                if (!canSpend)
                {
                    return BadRequest(new { error = "Недостаточно LW Coins для голосового ввода питания" });
                }

                string? audioFileId = null;
                string? audioUrl = null;
                double audioSizeMB = 0;

                if (saveAudio)
                {
                    try
                    {
                        audioFileId = await _voiceFileService.SaveVoiceFileAsync(audioFile, userId, "food");
                        audioUrl = _voiceFileService.GetDownloadUrl(audioFileId);
                        audioSizeMB = Math.Round(audioFile.Length / (1024.0 * 1024.0), 2);
                        _logger.LogInformation($"🗣️ Audio saved: {audioFileId} (Size: {audioSizeMB} MB)");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError($"❌ Failed to save audio: {ex.Message}");
                    }
                }

                using var memoryStream = new MemoryStream();
                await audioFile.CopyToAsync(memoryStream);
                var audioData = memoryStream.ToArray();

                _logger.LogInformation($"🗣️ Processing voice food for user {userId}, audio size: {audioData.Length} bytes");

                var result = await _geminiService.AnalyzeVoiceFoodAsync(audioData, mealType);

                if (!result.Success)
                {
                    if (!string.IsNullOrEmpty(audioFileId))
                    {
                        await _voiceFileService.DeleteVoiceFileAsync(userId, audioFileId);
                    }

                    return BadRequest(new { error = result.ErrorMessage });
                }

                var response = new VoiceFoodResponseWithAudio
                {
                    Success = result.Success,
                    TranscribedText = result.TranscribedText,
                    FoodItems = result.FoodItems,
                    EstimatedTotalCalories = result.EstimatedTotalCalories,

                    AudioUrl = audioUrl,
                    AudioFileId = audioFileId,
                    AudioSaved = !string.IsNullOrEmpty(audioFileId),
                    AudioSizeMB = audioSizeMB
                };

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

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError($"❌ Error processing voice food: {ex.Message}");
                return BadRequest(new { error = $"Ошибка обработки голосового ввода питания: {ex.Message}" });
            }
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

                var canSpend = await _lwCoinService.SpendLwCoinsAsync(userId, 1, "ai_food_scan",
                    "AI Food Scan", "photo");

                if (!canSpend)
                {
                    return BadRequest(new { error = "Недостаточно LW Coins для сканирования еды" });
                }

                var imageUrl = await _imageService.SaveImageAsync(image, "food-scans");

                using var memoryStream = new MemoryStream();
                await image.CopyToAsync(memoryStream);
                var imageData = memoryStream.ToArray();

                _logger.LogInformation($"🍎 Processing food scan for user {userId}, image size: {imageData.Length} bytes, saved at: {imageUrl}");

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
                                Image = imageUrl
                            }).ToList(),
                            DateTime = DateTime.UtcNow
                        };

                        await _foodIntakeService.AddFoodIntakeAsync(userId, addFoodRequest);
                        _logger.LogInformation($"✅ Saved {result.FoodItems.Count} food items for user {userId}");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError($"❌ Error saving food items: {ex.Message}");
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
        /// 💪 Анализ тела по фотографиям
        /// </summary>
        /// <param name="request">Данные для анализа тела</param>
        /// <returns>Результат анализа тела</returns>
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
                    if (!string.IsNullOrEmpty(frontImageUrl))
                        await _imageService.DeleteImageAsync(frontImageUrl);
                    if (!string.IsNullOrEmpty(sideImageUrl))
                        await _imageService.DeleteImageAsync(sideImageUrl);
                    if (!string.IsNullOrEmpty(backImageUrl))
                        await _imageService.DeleteImageAsync(backImageUrl);

                    return BadRequest(new { error = result.ErrorMessage });
                }

                result.FrontImageUrl = frontImageUrl;
                result.SideImageUrl = sideImageUrl;
                result.BackImageUrl = backImageUrl;

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
        /// 🧠 Проверка статуса ИИ сервиса
        /// </summary>
        /// <returns>Статус работы Gemini API</returns>
        [HttpGet("status")]
        [AllowAnonymous]
        [ProducesResponseType(200)]
        [ProducesResponseType(503)]
        public async Task<IActionResult> GetAIStatus()
        {
            try
            {
                var isWorking = await _geminiService.IsHealthyAsync();

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
                        "Voice Food Recognition",
                        "Voice File Storage & Management"
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

                var transactions = await _lwCoinService.GetUserLwCoinTransactionsAsync(userId);
                var aiTransactions = transactions.Where(t => t.FeatureUsed.StartsWith("ai_") ||
                                                           t.FeatureUsed == "photo" ||
                                                           t.FeatureUsed == "voice").ToList();

                var currentMonth = DateTime.UtcNow.Month;
                var currentYear = DateTime.UtcNow.Year;

                var monthlyUsage = aiTransactions.Where(t => t.CreatedAt.Month == currentMonth &&
                                                           t.CreatedAt.Year == currentYear).ToList();

                var voiceFilesStats = await _voiceFileService.GetVoiceFilesStatsAsync(userId);

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
                    VoiceFiles = new
                    {
                        TotalFiles = voiceFilesStats.TotalFiles,
                        WorkoutFiles = voiceFilesStats.WorkoutFiles,
                        FoodFiles = voiceFilesStats.FoodFiles,
                        TotalSizeMB = voiceFilesStats.TotalSizeMB,
                        FilesToday = voiceFilesStats.FilesToday,
                        FilesThisMonth = voiceFilesStats.FilesThisMonth
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
    public class VoiceWorkoutResponseWithFile : VoiceWorkoutResponse
    {
        public string? AudioFileId { get; set; }
        public string? AudioFileName { get; set; }
        public long? AudioFileSize { get; set; }
        public DateTime? AudioExpiresAt { get; set; }
        public string? DownloadUrl { get; set; }
    }

    public class VoiceFoodResponseWithFile : VoiceFoodResponse
    {
        public string? AudioFileId { get; set; }
        public string? AudioFileName { get; set; }
        public long? AudioFileSize { get; set; }
        public DateTime? AudioExpiresAt { get; set; }
        public string? DownloadUrl { get; set; }
    }
}
using FitnessTracker.API.DTOs;
using FitnessTracker.API.Services;
using FitnessTracker.API.Services.AI;
using FitnessTracker.API.Repositories;
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
        private readonly IUserRepository _userRepository;
        private readonly ILocalizationService _localizationService;
        private readonly ILogger<AIController> _logger;

        public AIController(
            IGeminiService geminiService,
            ILwCoinService lwCoinService,
            IFoodIntakeService foodIntakeService,
            IActivityService activityService,
            IBodyScanService bodyScanService,
            IImageService imageService,
            IVoiceFileService voiceFileService,
            IUserRepository userRepository,
            ILocalizationService localizationService,
            ILogger<AIController> logger)
        {
            _geminiService = geminiService;
            _lwCoinService = lwCoinService;
            _foodIntakeService = foodIntakeService;
            _activityService = activityService;
            _bodyScanService = bodyScanService;
            _imageService = imageService;
            _voiceFileService = voiceFileService;
            _userRepository = userRepository;
            _localizationService = localizationService;
            _logger = logger;
        }

        /// <summary>
        /// 🎤 Голосовой ввод тренировки (автоматически использует locale из профиля)
        /// </summary>
        [HttpPost("voice-workout")]
        [ProducesResponseType(typeof(VoiceWorkoutResponseWithAudio), 200)]
        [ProducesResponseType(400)]
        [ProducesResponseType(401)]
        public async Task<IActionResult> VoiceWorkout(
            IFormFile audioFile,
            [FromForm] string? workoutType = null,
            [FromForm] bool saveResults = false,
            [FromForm] bool saveAudio = true,
            [FromForm] string? locale = null)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized();

                var userLocale = locale ?? await _localizationService.GetUserLocaleAsync(userId);
                _logger.LogInformation($"🎤 Voice workout for user {userId} with locale: {userLocale}");

                if (audioFile == null || audioFile.Length == 0)
                {
                    var errorMsg = _localizationService.Translate("error.invalid_data", userLocale);
                    return BadRequest(new { error = errorMsg });
                }

                if (audioFile.Length > 50 * 1024 * 1024) // 50MB
                {
                    var errorMsg = _localizationService.Translate("error.file_too_large", userLocale);
                    return BadRequest(new { error = errorMsg });
                }

                var canSpend = await _lwCoinService.SpendLwCoinsAsync(userId, 1, "ai_voice_workout",
                    "AI Voice Workout", "voice");

                if (!canSpend)
                {
                    var errorMsg = _localizationService.Translate("error.insufficient_coins", userLocale);
                    return BadRequest(new { error = errorMsg });
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

                VoiceWorkoutResponse result;
                try
                {
                    result = await _geminiService.AnalyzeVoiceWorkoutAsync(audioData, workoutType, userLocale);
                }
                catch (Exception ex)
                {
                    _logger.LogError($"❌ Voice workout analysis failed: {ex.Message}");

                    if (!string.IsNullOrEmpty(audioFileId))
                    {
                        await _voiceFileService.DeleteVoiceFileAsync(userId, audioFileId);
                    }

                    var errorMsg = _localizationService.Translate("error.server_error", userLocale);
                    return BadRequest(new { error = errorMsg });
                }

                if (!result.Success)
                {
                    if (!string.IsNullOrEmpty(audioFileId))
                    {
                        await _voiceFileService.DeleteVoiceFileAsync(userId, audioFileId);
                    }

                    var errorMsg = _localizationService.Translate("error.analysis_failed", userLocale);
                    return BadRequest(new { error = result.ErrorMessage ?? errorMsg });
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

                if (saveResults && result.WorkoutData != null)
                {
                    try
                    {
                        var addActivityRequest = new AddActivityRequest
                        {
                            Type = result.WorkoutData.Type,
                            StartDate = result.WorkoutData.StartDate,
                            EndDate = result.WorkoutData.EndDate,
                            Calories = result.WorkoutData.Calories,
                            ActivityData = result.WorkoutData.ActivityData
                        };

                        await _activityService.AddActivityAsync(userId, addActivityRequest);
                        _logger.LogInformation($"✅ Saved voice workout to database for user {userId}");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError($"❌ Error saving voice workout: {ex.Message}");
                    }
                }

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError($"❌ Unexpected error: {ex.Message}");
                return BadRequest(new { error = ex.Message });
            }
        }

        /// <summary>
        /// 🗣️ Голосовой ввод питания (автоматически использует locale из профиля)
        /// </summary>
        [HttpPost("voice-food")]
        [ProducesResponseType(typeof(VoiceFoodResponseWithAudio), 200)]
        [ProducesResponseType(400)]
        [ProducesResponseType(401)]
        public async Task<IActionResult> VoiceFood(
            IFormFile audioFile,
            [FromForm] string? mealType = null,
            [FromForm] bool saveResults = false,
            [FromForm] bool saveAudio = true,
            [FromForm] string? locale = null)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized();

                var userLocale = locale ?? await _localizationService.GetUserLocaleAsync(userId);
                _logger.LogInformation($"🗣️ Voice food for user {userId} with locale: {userLocale}");

                var canSpend = await _lwCoinService.SpendLwCoinsAsync(userId, 1, "ai_voice_food",
                    "AI Voice Food", "voice");

                if (!canSpend)
                {
                    var errorMsg = _localizationService.Translate("error.insufficient_coins", userLocale);
                    return BadRequest(new { error = errorMsg });
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

                var result = await _geminiService.AnalyzeVoiceFoodAsync(audioData, mealType, userLocale);

                if (!result.Success)
                {
                    if (!string.IsNullOrEmpty(audioFileId))
                    {
                        await _voiceFileService.DeleteVoiceFileAsync(userId, audioFileId);
                    }

                    var errorMsg = _localizationService.Translate("error.analysis_failed", userLocale);
                    return BadRequest(new { error = result.ErrorMessage ?? errorMsg });
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
                return BadRequest(new { error = ex.Message });
            }
        }

        /// <summary>
        /// 🍎 Сканирование еды по фото (автоматически использует locale из профиля)
        /// </summary>
        [HttpPost("scan-food")]
        [ProducesResponseType(typeof(FoodScanResponse), 200)]
        [ProducesResponseType(400)]
        [ProducesResponseType(401)]
        public async Task<IActionResult> ScanFood(
            IFormFile image,
            [FromForm] string? userPrompt = null,
            [FromForm] bool saveResults = false,
            [FromForm] string? locale = null)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized();

                var userLocale = locale ?? await _localizationService.GetUserLocaleAsync(userId);
                _logger.LogInformation($"🍎 Food scan for user {userId} with locale: {userLocale}");

                var canSpend = await _lwCoinService.SpendLwCoinsAsync(userId, 1, "ai_food_scan",
                    "AI Food Scan", "photo");

                if (!canSpend)
                {
                    var errorMsg = _localizationService.Translate("error.insufficient_coins", userLocale);
                    return BadRequest(new { error = errorMsg });
                }

                var imageUrl = await _imageService.SaveImageAsync(image, "food-scans");

                using var memoryStream = new MemoryStream();
                await image.CopyToAsync(memoryStream);
                var imageData = memoryStream.ToArray();

                _logger.LogInformation($"🍎 Processing food scan, image saved at: {imageUrl}");

                var result = await _geminiService.AnalyzeFoodImageAsync(imageData, userPrompt, userLocale);

                if (!result.Success)
                {
                    await _imageService.DeleteImageAsync(imageUrl);
                    var errorMsg = _localizationService.Translate("error.analysis_failed", userLocale);
                    return BadRequest(new { error = result.ErrorMessage ?? errorMsg });
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
                return BadRequest(new { error = ex.Message });
            }
        }

        /// <summary>
        /// 💪 Анализ тела по фотографиям (автоматически использует locale из профиля)
        /// </summary>
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

                var locale = Request.Form["locale"].FirstOrDefault();
                var userLocale = locale ?? await _localizationService.GetUserLocaleAsync(userId);
                _logger.LogInformation($"💪 Body analysis for user {userId} with locale: {userLocale}");

                var user = await _userRepository.GetByIdAsync(userId);
                if (user == null)
                {
                    var errorMsg = _localizationService.Translate("error.user_not_found", userLocale);
                    return BadRequest(new { error = errorMsg });
                }

                var currentWeight = request.CurrentWeight ?? user.Weight;
                var height = request.Height ?? user.Height;
                var age = request.Age ?? user.Age;
                var gender = request.Gender ?? user.Gender;

                if (request.CurrentWeight.HasValue && request.CurrentWeight > 0 && request.CurrentWeight != user.Weight)
                {
                    user.Weight = request.CurrentWeight.Value;
                    await _userRepository.UpdateAsync(user);
                    _logger.LogInformation($"💪 Updated user weight: {user.Weight}kg");
                }

                if (request.FrontImage == null && request.SideImage == null && request.BackImage == null)
                {
                    var errorMsg = _localizationService.Translate("error.no_images", userLocale);
                    return BadRequest(new { error = errorMsg });
                }

                string? frontImageUrl = null;
                string? sideImageUrl = null;
                string? backImageUrl = null;

                byte[]? frontImageData = null;
                byte[]? sideImageData = null;
                byte[]? backImageData = null;

                try
                {
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
                }
                catch (Exception ex)
                {
                    _logger.LogError($"❌ Error saving images: {ex.Message}");
                    var errorMsg = _localizationService.Translate("error.image_save_failed", userLocale);
                    return BadRequest(new { error = errorMsg });
                }

                BodyScanResponse result;
                try
                {
                    result = await _geminiService.AnalyzeBodyImagesAsync(
                        frontImageData,
                        sideImageData,
                        backImageData,
                        currentWeight,
                        height,
                        age,
                        gender,
                        request.Goals,
                        userLocale);

                    _logger.LogInformation($"💪 Body analysis completed. Success: {result.Success}");
                }
                catch (Exception ex)
                {
                    _logger.LogError($"❌ Error during body analysis: {ex.Message}");

                    if (!string.IsNullOrEmpty(frontImageUrl))
                        await _imageService.DeleteImageAsync(frontImageUrl);
                    if (!string.IsNullOrEmpty(sideImageUrl))
                        await _imageService.DeleteImageAsync(sideImageUrl);
                    if (!string.IsNullOrEmpty(backImageUrl))
                        await _imageService.DeleteImageAsync(backImageUrl);

                    var errorMsg = _localizationService.Translate("error.analysis_failed", userLocale);
                    return BadRequest(new { error = errorMsg });
                }

                if (!result.Success)
                {
                    if (!string.IsNullOrEmpty(frontImageUrl))
                        await _imageService.DeleteImageAsync(frontImageUrl);
                    if (!string.IsNullOrEmpty(sideImageUrl))
                        await _imageService.DeleteImageAsync(sideImageUrl);
                    if (!string.IsNullOrEmpty(backImageUrl))
                        await _imageService.DeleteImageAsync(backImageUrl);

                    var errorMsg = _localizationService.Translate("error.body_analysis_failed", userLocale);
                    return BadRequest(new { error = result.ErrorMessage ?? errorMsg });
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
                        Weight = currentWeight,
                        BodyFatPercentage = result.BodyAnalysis.EstimatedBodyFatPercentage,
                        MusclePercentage = result.BodyAnalysis.EstimatedMusclePercentage,
                        WaistCircumference = result.BodyAnalysis.EstimatedWaistCircumference,
                        ChestCircumference = result.BodyAnalysis.EstimatedChestCircumference,
                        HipCircumference = result.BodyAnalysis.EstimatedHipCircumference,
                        BasalMetabolicRate = result.BodyAnalysis.BasalMetabolicRate,
                        MetabolicRateCategory = result.BodyAnalysis.MetabolicRateCategory,
                        Notes = $"AI Analysis: {result.BodyAnalysis.OverallCondition}",
                        ScanDate = DateTime.UtcNow
                    };

                    await _bodyScanService.AddBodyScanAsync(userId, addBodyScanRequest);
                    _logger.LogInformation($"✅ Body scan saved for user {userId}");
                }
                catch (Exception ex)
                {
                    _logger.LogError($"❌ Error saving body scan: {ex.Message}");
                }

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError($"❌ Unexpected error in body analysis: {ex.Message}");
                return BadRequest(new { error = ex.Message });
            }
        }

        /// <summary>
        /// 📝 Текстовый ввод тренировки (автоматически использует locale из профиля)
        /// </summary>
        [HttpPost("text-workout")]
        [ProducesResponseType(typeof(TextWorkoutResponse), 200)]
        [ProducesResponseType(400)]
        [ProducesResponseType(401)]
        public async Task<IActionResult> TextWorkout([FromBody] TextWorkoutRequest request)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized();

                var user = await _userRepository.GetByIdAsync(userId);
                if (user == null)
                {
                    return BadRequest(new { error = "User not found" });
                }

                var userLocale = user.Locale ?? "en"; 

                _logger.LogInformation($"📝 Text workout for user {userId} with profile locale: {userLocale}");

                if (string.IsNullOrWhiteSpace(request.WorkoutDescription))
                {
                    var errorMsg = userLocale.StartsWith("ru")
                        ? "Описание тренировки не предоставлено"
                        : "Workout description is required";
                    return BadRequest(new { error = errorMsg });
                }

                var canSpend = await _lwCoinService.SpendLwCoinsAsync(userId, 1, "ai_text_workout",
                    "AI Text Workout", "text");

                if (!canSpend)
                {
                    var errorMsg = userLocale.StartsWith("ru")
                        ? "Недостаточно LW Coins"
                        : "Insufficient LW Coins";
                    return BadRequest(new { error = errorMsg });
                }

                var result = await _geminiService.AnalyzeTextWorkoutAsync(
                    request.WorkoutDescription,
                    request.WorkoutType,
                    userLocale 
                );

                if (!result.Success)
                {
                    var errorMsg = userLocale.StartsWith("ru")
                        ? "Ошибка анализа"
                        : "Analysis failed";
                    return BadRequest(new { error = result.ErrorMessage ?? errorMsg });
                }

                if (request.SaveResults && result.WorkoutData != null)
                {
                    try
                    {
                        var addActivityRequest = new AddActivityRequest
                        {
                            Type = result.WorkoutData.Type,
                            StartDate = result.WorkoutData.StartDate,
                            EndDate = result.WorkoutData.EndDate,
                            Calories = result.WorkoutData.Calories,
                            ActivityData = result.WorkoutData.ActivityData
                        };

                        await _activityService.AddActivityAsync(userId, addActivityRequest);
                        _logger.LogInformation($"✅ Saved text workout to database for user {userId}");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError($"❌ Error saving text workout: {ex.Message}");
                    }
                }

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError($"❌ Unexpected error processing text workout: {ex.Message}");
                return BadRequest(new { error = ex.Message });
            }
        }

        /// <summary>
        /// 📝 Текстовый ввод питания (автоматически использует locale из профиля)
        /// </summary>
        [HttpPost("text-food")]
        [ProducesResponseType(typeof(TextFoodResponse), 200)]
        [ProducesResponseType(400)]
        [ProducesResponseType(401)]
        public async Task<IActionResult> TextFood([FromBody] TextFoodRequest request)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized();

                // Получаем locale из профиля
                var userLocale = request.Locale ?? await _localizationService.GetUserLocaleAsync(userId);
                _logger.LogInformation($"📝 Text food for user {userId} with locale: {userLocale}");

                if (string.IsNullOrWhiteSpace(request.FoodDescription))
                {
                    var errorMsg = _localizationService.Translate("error.invalid_data", userLocale);
                    return BadRequest(new { error = errorMsg });
                }

                var canSpend = await _lwCoinService.SpendLwCoinsAsync(userId, 1, "ai_text_food",
                    "AI Text Food", "text");

                if (!canSpend)
                {
                    var errorMsg = _localizationService.Translate("error.insufficient_coins", userLocale);
                    return BadRequest(new { error = errorMsg });
                }

                // Передаем locale в сервис
                var result = await _geminiService.AnalyzeTextFoodAsync(request.FoodDescription, request.MealType, userLocale);

                if (!result.Success)
                {
                    var errorMsg = _localizationService.Translate("error.analysis_failed", userLocale);
                    return BadRequest(new { error = result.ErrorMessage ?? errorMsg });
                }

                if (request.SaveResults && result.FoodItems?.Any() == true)
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
                        _logger.LogInformation($"✅ Saved {result.FoodItems.Count} text food items for user {userId}");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError($"❌ Error saving text food: {ex.Message}");
                    }
                }

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError($"❌ Error processing text food: {ex.Message}");
                return BadRequest(new { error = ex.Message });
            }
        }

        /// <summary>
        /// 🔧 Коррекция продукта с указанием ингредиентов/начинки
        /// </summary>
        [HttpPost("correct-food")]
        public async Task<IActionResult> CorrectFood([FromBody] FoodCorrectionRequest request)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized();

                var userLocale = request.Locale ?? await _localizationService.GetUserLocaleAsync(userId);
                _logger.LogInformation($"🔧 Food correction for user {userId} with locale: {userLocale}");

                if (string.IsNullOrWhiteSpace(request.CorrectionText))
                {
                    return BadRequest(new { error = "Текст коррекции не предоставлен" });
                }

                if (string.IsNullOrWhiteSpace(request.FoodItem?.Name))
                {
                    return BadRequest(new { error = "Данные о блюде не предоставлены" });
                }

                var canSpend = await _lwCoinService.SpendLwCoinsAsync(userId, 1, "ai_food_correction",
                    "AI Food Correction", "text");

                if (!canSpend)
                {
                    var errorMsg = _localizationService.Translate("error.insufficient_coins", userLocale);
                    return BadRequest(new { error = errorMsg });
                }

                var result = await _geminiService.CorrectFoodItemAsync(request.FoodItem.Name, request.CorrectionText, userLocale);

                if (!result.Success)
                {
                    var errorMsg = _localizationService.Translate("error.analysis_failed", userLocale);
                    return BadRequest(new { error = result.ErrorMessage ?? errorMsg });
                }

                if (request.SaveResults)
                {
                    try
                    {
                        var addFoodRequest = new AddFoodIntakeRequest
                        {
                            Items = new List<FoodItemRequest>
                            {
                                new FoodItemRequest
                                {
                                    Name = result.CorrectedFoodItem.Name,
                                    Weight = result.CorrectedFoodItem.EstimatedWeight,
                                    WeightType = result.CorrectedFoodItem.WeightType,
                                    NutritionPer100g = result.CorrectedFoodItem.NutritionPer100g
                                }
                            },
                            DateTime = DateTime.UtcNow
                        };

                        await _foodIntakeService.AddFoodIntakeAsync(userId, addFoodRequest);
                        _logger.LogInformation($"✅ Saved corrected food for user {userId}");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError($"❌ Error saving: {ex.Message}");
                    }
                }

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError($"❌ Error: {ex.Message}");
                return BadRequest(new { error = "Системная ошибка" });
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
                    Service = "Gemini 2.5 Flash",
                    Status = isWorking ? "Online" : "Offline",
                    Timestamp = DateTime.UtcNow,
                    ModelVersion = "gemini-2.5-flash",
                    Features = new[]
                    {
                        "Food Image Analysis",
                        "Body Analysis",
                        "Voice Workout Recognition",
                        "Voice Food Recognition",
                        "Voice File Storage & Management",
                        "Enhanced AI Capabilities with Gemini 2.5"
                    },
                    ModelInfo = new
                    {
                        Name = "Gemini 2.5 Flash",
                        Version = "2.5",
                        Provider = "Google Vertex AI",
                        Capabilities = new[]
                        {
                            "Multimodal (Text, Image, Audio)",
                            "Improved Accuracy",
                            "Faster Response Times",
                            "Better Russian Language Support"
                        }
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError($"❌ AI Status check failed: {ex.Message}");
                return StatusCode(503, new
                {
                    Service = "Gemini 2.5 Flash",
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
                    },
                    MonthlyFeatureUsage = new
                    {
                        FoodScans = monthlyUsage.Count(t => t.FeatureUsed == "photo" || t.FeatureUsed == "ai_food_scan"),
                        VoiceWorkouts = monthlyUsage.Count(t => t.FeatureUsed == "ai_voice_workout"),
                        VoiceFood = monthlyUsage.Count(t => t.FeatureUsed == "ai_voice_food"),
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
                    FreeFeatures = new
                    {
                        BodyAnalysisCount = "Неограниченно (бесплатно)",
                        TotalBodyAnalysis = "Статистика недоступна для бесплатных функций"
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
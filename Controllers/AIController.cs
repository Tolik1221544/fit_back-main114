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
        private readonly IAudioFileService _audioFileService; // НОВОЕ
        private readonly ILogger<AIController> _logger;

        public AIController(
            IGeminiService geminiService,
            ILwCoinService lwCoinService,
            IFoodIntakeService foodIntakeService,
            IActivityService activityService,
            IBodyScanService bodyScanService,
            IImageService imageService,
            IAudioFileService audioFileService, // НОВОЕ
            ILogger<AIController> logger)
        {
            _geminiService = geminiService;
            _lwCoinService = lwCoinService;
            _foodIntakeService = foodIntakeService;
            _activityService = activityService;
            _bodyScanService = bodyScanService;
            _imageService = imageService;
            _audioFileService = audioFileService; // НОВОЕ
            _logger = logger;
        }

        /// <summary>
        /// 🎤 Голосовой ввод тренировки с сохранением файла (требует LW Coins)
        /// </summary>
        /// <param name="audioFile">Аудиофайл с описанием тренировки</param>
        /// <param name="workoutType">Тип тренировки (strength/cardio)</param>
        /// <param name="saveResults">Сохранить результаты в базу данных</param>
        /// <param name="keepAudioFile">Сохранить аудио файл на сервере (по умолчанию 1 час)</param>
        /// <returns>Распознанная и структурированная информация о тренировке + информация о сохраненном файле</returns>
        /// <response code="200">Тренировка успешно распознана</response>
        /// <response code="400">Недостаточно LW Coins или ошибка обработки</response>
        /// <response code="401">Требуется авторизация</response>
        [HttpPost("voice-workout")]
        public async Task<IActionResult> VoiceWorkout(
            IFormFile audioFile,
            [FromForm] string? workoutType = null,
            [FromForm] bool saveResults = false,
            [FromForm] bool keepAudioFile = true) // НОВОЕ
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

                // НОВОЕ: Сохраняем аудио файл на сервере
                VoiceWorkoutResponseWithFile? responseWithFile = null;
                if (keepAudioFile)
                {
                    var audioSaveResult = await _audioFileService.SaveAudioFileAsync(audioFile, userId, 1); // 1 час
                    if (!audioSaveResult.IsSuccess)
                    {
                        _logger.LogWarning($"Failed to save audio file: {audioSaveResult.ErrorMessage}");
                    }
                    else
                    {
                        responseWithFile = new VoiceWorkoutResponseWithFile
                        {
                            AudioFileId = audioSaveResult.FileInfo!.FileId,
                            AudioFileName = audioSaveResult.FileInfo.OriginalName,
                            AudioFileSize = audioSaveResult.FileInfo.FileSize,
                            AudioExpiresAt = audioSaveResult.FileInfo.ExpiresAt,
                            DownloadUrl = $"/api/ai/download-audio/{audioSaveResult.FileInfo.FileId}"
                        };
                    }
                }

                // Конвертируем аудио в байты для анализа
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

                // Объединяем результаты
                if (responseWithFile != null)
                {
                    responseWithFile.Success = result.Success;
                    responseWithFile.ErrorMessage = result.ErrorMessage;
                    responseWithFile.TranscribedText = result.TranscribedText;
                    responseWithFile.WorkoutData = result.WorkoutData;
                    return Ok(responseWithFile);
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
        /// 🗣️ Голосовой ввод питания с сохранением файла (требует LW Coins)
        /// </summary>
        /// <param name="audioFile">Аудиофайл с описанием еды</param>
        /// <param name="mealType">Тип приема пищи</param>
        /// <param name="saveResults">Сохранить результаты в базу данных</param>
        /// <param name="keepAudioFile">Сохранить аудио файл на сервере</param>
        /// <returns>Распознанная и структурированная информация о питании + информация о файле</returns>
        /// <response code="200">Питание успешно распознано</response>
        /// <response code="400">Недостаточно LW Coins или ошибка обработки</response>
        /// <response code="401">Требуется авторизация</response>
        [HttpPost("voice-food")]
        [ProducesResponseType(typeof(VoiceFoodResponseWithFile), 200)]
        [ProducesResponseType(400)]
        [ProducesResponseType(401)]
        public async Task<IActionResult> VoiceFood(
            IFormFile audioFile,
            [FromForm] string? mealType = null,
            [FromForm] bool saveResults = false,
            [FromForm] bool keepAudioFile = true) // НОВОЕ
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

                // НОВОЕ: Сохраняем аудио файл
                VoiceFoodResponseWithFile? responseWithFile = null;
                if (keepAudioFile)
                {
                    var audioSaveResult = await _audioFileService.SaveAudioFileAsync(audioFile, userId, 1);
                    if (audioSaveResult.IsSuccess)
                    {
                        responseWithFile = new VoiceFoodResponseWithFile
                        {
                            AudioFileId = audioSaveResult.FileInfo!.FileId,
                            AudioFileName = audioSaveResult.FileInfo.OriginalName,
                            AudioFileSize = audioSaveResult.FileInfo.FileSize,
                            AudioExpiresAt = audioSaveResult.FileInfo.ExpiresAt,
                            DownloadUrl = $"/api/ai/download-audio/{audioSaveResult.FileInfo.FileId}"
                        };
                    }
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

                // Объединяем результаты
                if (responseWithFile != null)
                {
                    responseWithFile.Success = result.Success;
                    responseWithFile.ErrorMessage = result.ErrorMessage;
                    responseWithFile.TranscribedText = result.TranscribedText;
                    responseWithFile.FoodItems = result.FoodItems;
                    responseWithFile.EstimatedTotalCalories = result.EstimatedTotalCalories;
                    return Ok(responseWithFile);
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
        /// 📥 Скачать сохраненный аудио файл (любой пользователь может скачать любой файл)
        /// </summary>
        /// <param name="fileId">ID аудио файла</param>
        /// <returns>Аудио файл для скачивания</returns>
        [HttpGet("download-audio/{fileId}")]
        [ProducesResponseType(typeof(FileResult), 200)]
        [ProducesResponseType(404)]
        [ProducesResponseType(401)]
        public async Task<IActionResult> DownloadAudio(string fileId)
        {
            try
            {
                var currentUserId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(currentUserId))
                    return Unauthorized();

                var audioInfo = await _audioFileService.GetAudioFileInfoAsync(fileId);
                if (audioInfo == null)
                    return NotFound(new { error = "Audio file not found or expired" });

                // ✅ УБРАНА ПРОВЕРКА ВЛАДЕЛЬЦА - теперь любой пользователь может скачать любой файл
                // if (audioInfo.UserId != currentUserId)
                //     return Forbid();

                var audioData = await _audioFileService.GetAudioFileAsync(fileId);
                if (audioData == null)
                    return NotFound(new { error = "Audio file data not found" });

                _logger.LogInformation($"📥 Downloaded audio file: {fileId} by user {currentUserId} (owner: {audioInfo.UserId})");

                // Добавляем информацию о владельце в заголовки (опционально)
                Response.Headers.Add("X-File-Owner", audioInfo.UserId);
                Response.Headers.Add("X-File-Created", audioInfo.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss"));

                return File(audioData, audioInfo.MimeType, audioInfo.OriginalName);
            }
            catch (Exception ex)
            {
                _logger.LogError($"❌ Error downloading audio file {fileId}: {ex.Message}");
                return BadRequest(new { error = "Failed to download audio file" });
            }
        }

        /// <summary>
        /// 📋 Получить список сохраненных аудио файлов пользователя
        /// </summary>
        /// <returns>Список аудио файлов пользователя</returns>
        [HttpGet("audio-files")]
        [ProducesResponseType(200)]
        [ProducesResponseType(401)]
        public async Task<IActionResult> GetUserAudioFiles()
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized();

                var audioFiles = await _audioFileService.GetUserAudioFilesAsync(userId);

                var response = audioFiles.Select(file => new
                {
                    fileId = file.FileId,
                    fileName = file.OriginalName,
                    fileSize = file.FileSize,
                    mimeType = file.MimeType,
                    userId = file.UserId, // Показываем владельца
                    createdAt = file.CreatedAt,
                    expiresAt = file.ExpiresAt,
                    downloadUrl = $"/api/ai/download-audio/{file.FileId}",
                    isExpired = file.ExpiresAt < DateTime.UtcNow,
                    isOwner = file.UserId == userId
                });

                return Ok(new
                {
                    files = response,
                    totalFiles = audioFiles.Count,
                    scope = "user",
                    note = "Your personal audio files. Use /api/ai/audio-files/all for all files."
                });
            }
            catch (Exception ex)
            {
                _logger.LogError($"❌ Error getting user audio files: {ex.Message}");
                return BadRequest(new { error = "Failed to get audio files" });
            }
        }

        /// <summary>
        /// 📋 Получить список ВСЕХ аудио файлов на сервере
        /// </summary>
        /// <param name="includeExpired">Включить истекшие файлы</param>
        /// <returns>Список всех аудио файлов</returns>
        [HttpGet("audio-files/all")]
        [ProducesResponseType(200)]
        [ProducesResponseType(401)]
        public async Task<IActionResult> GetAllAudioFiles([FromQuery] bool includeExpired = false)
        {
            try
            {
                var currentUserId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(currentUserId))
                    return Unauthorized();

                var allAudioFiles = await _audioFileService.GetAllAudioFilesAsync(includeExpired);

                var response = allAudioFiles.Select(file => new
                {
                    fileId = file.FileId,
                    fileName = file.OriginalName,
                    fileSize = file.FileSize,
                    mimeType = file.MimeType,
                    userId = file.UserId,
                    createdAt = file.CreatedAt,
                    expiresAt = file.ExpiresAt,
                    downloadUrl = $"/api/ai/download-audio/{file.FileId}",
                    isExpired = file.ExpiresAt < DateTime.UtcNow,
                    isOwner = file.UserId == currentUserId,
                    // Маскируем userId для приватности (показываем только первые 4 символа)
                    ownerMask = file.UserId.Substring(0, Math.Min(4, file.UserId.Length)) + "****"
                });

                return Ok(new
                {
                    files = response,
                    totalFiles = allAudioFiles.Count,
                    scope = "global",
                    includeExpired = includeExpired,
                    note = "All audio files on server. You can download any file using /api/ai/download-audio/{fileId}"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError($"❌ Error getting all audio files: {ex.Message}");
                return BadRequest(new { error = "Failed to get all audio files" });
            }
        }

        /// <summary>
        /// 🗑️ Удалить сохраненный аудио файл
        /// </summary>
        /// <param name="fileId">ID файла для удаления</param>
        /// <returns>Результат удаления</returns>
        [HttpDelete("audio-files/{fileId}")]
        [ProducesResponseType(200)]
        [ProducesResponseType(404)]
        [ProducesResponseType(403)]
        public async Task<IActionResult> DeleteAudioFile(string fileId)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized();

                var audioInfo = await _audioFileService.GetAudioFileInfoAsync(fileId);
                if (audioInfo == null)
                    return NotFound(new { error = "Audio file not found" });

                // Проверяем права доступа
                if (audioInfo.UserId != userId)
                    return Forbid();

                var deleted = await _audioFileService.DeleteAudioFileAsync(fileId);
                if (deleted)
                {
                    return Ok(new { success = true, message = "Audio file deleted successfully" });
                }
                else
                {
                    return BadRequest(new { error = "Failed to delete audio file" });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"❌ Error deleting audio file {fileId}: {ex.Message}");
                return BadRequest(new { error = "Failed to delete audio file" });
            }
        }

        /// <summary>
        /// 🧹 Очистить истекшие аудио файлы (админ функция)
        /// </summary>
        /// <returns>Количество удаленных файлов</returns>
        [HttpPost("cleanup-audio")]
        [ProducesResponseType(200)]
        public async Task<IActionResult> CleanupExpiredAudioFiles()
        {
            try
            {
                var deletedCount = await _audioFileService.CleanupExpiredFilesAsync();

                return Ok(new
                {
                    success = true,
                    deletedFiles = deletedCount,
                    message = $"Cleaned up {deletedCount} expired audio files"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError($"❌ Error cleaning up audio files: {ex.Message}");
                return BadRequest(new { error = "Failed to cleanup audio files" });
            }
        }

        // Остальные методы остаются без изменений...

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

                // Проверяем и тратим LW Coins
                var canSpend = await _lwCoinService.SpendLwCoinsAsync(userId, 1, "ai_food_scan",
                    "AI Food Scan", "photo");

                if (!canSpend)
                {
                    return BadRequest(new { error = "Недостаточно LW Coins для сканирования еды" });
                }

                // Сохраняем изображение и получаем URL
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

                // Добавляем URL изображения в ответ
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

                // Сохраняем изображения и получаем URLs
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

                // Добавляем URLs изображений в ответ
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
                        "Audio File Storage"
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
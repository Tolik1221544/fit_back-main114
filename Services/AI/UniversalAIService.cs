using FitnessTracker.API.DTOs;
using FitnessTracker.API.Services.AI;

namespace FitnessTracker.API.Services.AI
{
    
    public class UniversalAIService : IGeminiService
    {
        private readonly IAIProvider _primaryProvider;
        private readonly IAIErrorHandlerService _errorHandler;
        private readonly ILogger<UniversalAIService> _logger;
        private readonly IConfiguration _configuration;

        public UniversalAIService(
            IAIProvider primaryProvider,
            IAIErrorHandlerService errorHandler,
            ILogger<UniversalAIService> logger,
            IConfiguration configuration)
        {
            _primaryProvider = primaryProvider;
            _errorHandler = errorHandler;
            _logger = logger;
            _configuration = configuration;
        }

        public async Task<FoodScanResponse> AnalyzeFoodImageAsync(byte[] imageData, string? userPrompt = null)
        {
            const int maxAttempts = 3;
            var lastException = new Exception("Unknown error");

            for (int attempt = 1; attempt <= maxAttempts; attempt++)
            {
                try
                {
                    _logger.LogInformation($"🍎 Food analysis attempt {attempt}/{maxAttempts}");

                    var result = await _primaryProvider.AnalyzeFoodImageAsync(imageData, userPrompt);

                    if (result.Success && ValidateFoodScanResult(result))
                    {
                        _logger.LogInformation($"✅ Food analysis successful on attempt {attempt}");
                        return result;
                    }

                    if (result.Success && !ValidateFoodScanResult(result))
                    {
                        _logger.LogWarning($"⚠️ Food analysis returned invalid data on attempt {attempt}");
                        if (attempt == maxAttempts)
                        {
                            return _errorHandler.CreateFallbackFoodResponse("Invalid analysis result", imageData);
                        }
                    }
                    else
                    {
                        _logger.LogWarning($"❌ Food analysis failed on attempt {attempt}: {result.ErrorMessage}");
                    }
                }
                catch (Exception ex)
                {
                    lastException = ex;
                    _logger.LogError($"❌ Food analysis error on attempt {attempt}: {ex.Message}");

                    if (!_errorHandler.ShouldRetryRequest(ex, attempt))
                    {
                        break;
                    }

                    if (attempt < maxAttempts)
                    {
                        var delay = TimeSpan.FromSeconds(Math.Pow(2, attempt)); // Exponential backoff
                        _logger.LogInformation($"⏳ Retrying in {delay.TotalSeconds} seconds...");
                        await Task.Delay(delay);
                    }
                }
            }

            _logger.LogError($"❌ All food analysis attempts failed. Creating fallback response.");
            return _errorHandler.CreateFallbackFoodResponse($"Analysis failed after {maxAttempts} attempts: {lastException.Message}", imageData);
        }

        public async Task<BodyScanResponse> AnalyzeBodyImagesAsync(
            byte[]? frontImageData,
            byte[]? sideImageData,
            byte[]? backImageData,
            decimal? weight = null,
            decimal? height = null,
            int? age = null,
            string? gender = null,
            string? goals = null)
        {
            const int maxAttempts = 2;
            var lastException = new Exception("Unknown error");

            for (int attempt = 1; attempt <= maxAttempts; attempt++)
            {
                try
                {
                    _logger.LogInformation($"💪 Body analysis attempt {attempt}/{maxAttempts}");

                    var result = await _primaryProvider.AnalyzeBodyImagesAsync(
                        frontImageData, sideImageData, backImageData, weight, height, age, gender, goals);

                    if (result.Success && ValidateBodyScanResult(result))
                    {
                        _logger.LogInformation($"✅ Body analysis successful on attempt {attempt}");
                        return result;
                    }

                    _logger.LogWarning($"❌ Body analysis failed on attempt {attempt}: {result.ErrorMessage}");
                }
                catch (Exception ex)
                {
                    lastException = ex;
                    _logger.LogError($"❌ Body analysis error on attempt {attempt}: {ex.Message}");

                    if (!_errorHandler.ShouldRetryRequest(ex, attempt))
                    {
                        break;
                    }

                    if (attempt < maxAttempts)
                    {
                        await Task.Delay(TimeSpan.FromSeconds(3));
                    }
                }
            }

            _logger.LogError($"❌ All body analysis attempts failed. Creating fallback response.");
            return _errorHandler.CreateFallbackBodyResponse($"Analysis failed: {lastException.Message}");
        }

        public async Task<VoiceWorkoutResponse> AnalyzeVoiceWorkoutAsync(byte[] audioData, string? workoutType = null)
        {
            const int maxAttempts = 2;
            var lastException = new Exception("Unknown error");

            for (int attempt = 1; attempt <= maxAttempts; attempt++)
            {
                try
                {
                    _logger.LogInformation($"🎤 Voice workout analysis attempt {attempt}/{maxAttempts}");

                    var result = await _primaryProvider.AnalyzeVoiceWorkoutAsync(audioData, workoutType);

                    if (result.Success && ValidateVoiceWorkoutResult(result))
                    {
                        _logger.LogInformation($"✅ Voice workout analysis successful on attempt {attempt}");
                        return result;
                    }

                    _logger.LogWarning($"❌ Voice workout analysis failed on attempt {attempt}: {result.ErrorMessage}");
                }
                catch (Exception ex)
                {
                    lastException = ex;
                    _logger.LogError($"❌ Voice workout analysis error on attempt {attempt}: {ex.Message}");

                    if (!_errorHandler.ShouldRetryRequest(ex, attempt))
                    {
                        break;
                    }

                    if (attempt < maxAttempts)
                    {
                        await Task.Delay(TimeSpan.FromSeconds(2));
                    }
                }
            }

            _logger.LogError($"❌ All voice workout attempts failed. Creating fallback response.");
            return _errorHandler.CreateFallbackWorkoutResponse($"Analysis failed: {lastException.Message}", workoutType);
        }

        public async Task<VoiceFoodResponse> AnalyzeVoiceFoodAsync(byte[] audioData, string? mealType = null)
        {
            const int maxAttempts = 2;
            var lastException = new Exception("Unknown error");

            for (int attempt = 1; attempt <= maxAttempts; attempt++)
            {
                try
                {
                    _logger.LogInformation($"🗣️ Voice food analysis attempt {attempt}/{maxAttempts}");

                    var result = await _primaryProvider.AnalyzeVoiceFoodAsync(audioData, mealType);

                    if (result.Success && ValidateVoiceFoodResult(result))
                    {
                        _logger.LogInformation($"✅ Voice food analysis successful on attempt {attempt}");
                        return result;
                    }

                    _logger.LogWarning($"❌ Voice food analysis failed on attempt {attempt}: {result.ErrorMessage}");
                }
                catch (Exception ex)
                {
                    lastException = ex;
                    _logger.LogError($"❌ Voice food analysis error on attempt {attempt}: {ex.Message}");

                    if (!_errorHandler.ShouldRetryRequest(ex, attempt))
                    {
                        break;
                    }

                    if (attempt < maxAttempts)
                    {
                        await Task.Delay(TimeSpan.FromSeconds(2));
                    }
                }
            }

            _logger.LogError($"❌ All voice food attempts failed. Creating fallback response.");
            return _errorHandler.CreateFallbackVoiceFoodResponse($"Analysis failed: {lastException.Message}", mealType);
        }

        public async Task<TextWorkoutResponse> AnalyzeTextWorkoutAsync(string workoutText, string? workoutType = null)
        {
            const int maxAttempts = 2;
            var lastException = new Exception("Unknown error");

            for (int attempt = 1; attempt <= maxAttempts; attempt++)
            {
                try
                {
                    _logger.LogInformation($"📝 Text workout analysis attempt {attempt}/{maxAttempts}");

                    var result = await _primaryProvider.AnalyzeTextWorkoutAsync(workoutText, workoutType);

                    if (result.Success && ValidateTextWorkoutResult(result))
                    {
                        _logger.LogInformation($"✅ Text workout analysis successful on attempt {attempt}");
                        return result;
                    }

                    _logger.LogWarning($"❌ Text workout analysis failed on attempt {attempt}: {result.ErrorMessage}");
                }
                catch (Exception ex)
                {
                    lastException = ex;
                    _logger.LogError($"❌ Text workout analysis error on attempt {attempt}: {ex.Message}");

                    if (!_errorHandler.ShouldRetryRequest(ex, attempt))
                    {
                        break;
                    }

                    if (attempt < maxAttempts)
                    {
                        await Task.Delay(TimeSpan.FromSeconds(2));
                    }
                }
            }

            _logger.LogError($"❌ All text workout attempts failed. Creating fallback response.");
            return _errorHandler.CreateFallbackTextWorkoutResponse($"Analysis failed: {lastException.Message}", workoutType);
        }

        public async Task<TextFoodResponse> AnalyzeTextFoodAsync(string foodText, string? mealType = null)
        {
            const int maxAttempts = 2;
            var lastException = new Exception("Unknown error");

            for (int attempt = 1; attempt <= maxAttempts; attempt++)
            {
                try
                {
                    _logger.LogInformation($"📝 Text food analysis attempt {attempt}/{maxAttempts}");

                    var result = await _primaryProvider.AnalyzeTextFoodAsync(foodText, mealType);

                    if (result.Success && ValidateTextFoodResult(result))
                    {
                        _logger.LogInformation($"✅ Text food analysis successful on attempt {attempt}");
                        return result;
                    }

                    _logger.LogWarning($"❌ Text food analysis failed on attempt {attempt}: {result.ErrorMessage}");
                }
                catch (Exception ex)
                {
                    lastException = ex;
                    _logger.LogError($"❌ Text food analysis error on attempt {attempt}: {ex.Message}");

                    if (!_errorHandler.ShouldRetryRequest(ex, attempt))
                    {
                        break;
                    }

                    if (attempt < maxAttempts)
                    {
                        await Task.Delay(TimeSpan.FromSeconds(2));
                    }
                }
            }

            _logger.LogError($"❌ All text food attempts failed. Creating fallback response.");
            return _errorHandler.CreateFallbackTextFoodResponse($"Analysis failed: {lastException.Message}", mealType);
        }

        public async Task<FoodCorrectionResponse> CorrectFoodItemAsync(string originalFoodName, string correctionText)
        {
            try
            {
                _logger.LogInformation($"🔧 Food correction: {originalFoodName} + {correctionText}");

                var result = await _primaryProvider.CorrectFoodItemAsync(originalFoodName, correctionText);

                if (result.Success)
                {
                    _logger.LogInformation($"✅ Food correction successful");
                    return result;
                }

                _logger.LogWarning($"❌ Food correction failed: {result.ErrorMessage}");
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError($"❌ Food correction error: {ex.Message}");
                return new FoodCorrectionResponse
                {
                    Success = false,
                    ErrorMessage = $"Системная ошибка: {ex.Message}"
                };
            }
        }

        private bool ValidateTextWorkoutResult(TextWorkoutResponse result)
        {
            if (result?.WorkoutData == null)
                return false;

            return !string.IsNullOrEmpty(result.WorkoutData.Type) &&
                   result.WorkoutData.StartDate != default; 
        }

        private bool ValidateTextFoodResult(TextFoodResponse result)
        {
            if (result?.FoodItems == null || !result.FoodItems.Any())
                return false;

            return result.FoodItems.All(item =>
                !string.IsNullOrEmpty(item.Name) &&
                item.EstimatedWeight > 0);
        }

        public async Task<bool> IsHealthyAsync()
        {
            try
            {
                return await _primaryProvider.IsHealthyAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError($"❌ Health check failed: {ex.Message}");
                return false;
            }
        }

        public async Task<Dictionary<string, bool>> GetProviderHealthStatusAsync()
        {
            var status = new Dictionary<string, bool>();

            try
            {
                status[_primaryProvider.ProviderName] = await _primaryProvider.IsHealthyAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError($"❌ Provider health check failed for {_primaryProvider.ProviderName}: {ex.Message}");
                status[_primaryProvider.ProviderName] = false;
            }

            return status;
        }

        // Legacy methods for backward compatibility
        public async Task<GeminiResponse> SendGeminiRequestAsync(List<GeminiContent> contents, GeminiGenerationConfig? config = null)
        {
            throw new NotImplementedException("Use specific analysis methods instead");
        }

        public string ConvertImageToBase64(byte[] imageData, string mimeType)
        {
            return Convert.ToBase64String(imageData);
        }

        public async Task<bool> ValidateImageQualityAsync(byte[] imageData)
        {
            try
            {
                if (imageData == null || imageData.Length == 0)
                    return false;

                if (imageData.Length > 10 * 1024 * 1024) // 10MB
                    return false;

                var imageFormats = new byte[][]
                {
                    new byte[] { 0xFF, 0xD8 }, // JPEG
                    new byte[] { 0x89, 0x50, 0x4E, 0x47 }, // PNG
                    new byte[] { 0x47, 0x49, 0x46 } // GIF
                };

                return imageFormats.Any(format =>
                    imageData.Take(format.Length).SequenceEqual(format));
            }
            catch
            {
                return false;
            }
        }

        // Private validation methods
        private bool ValidateFoodScanResult(FoodScanResponse result)
        {
            if (result?.FoodItems == null || !result.FoodItems.Any())
                return false;

            return result.FoodItems.All(item =>
                !string.IsNullOrEmpty(item.Name) &&
                item.EstimatedWeight > 0 &&
                item.NutritionPer100g != null &&
                item.NutritionPer100g.Calories > 0);
        }

        private bool ValidateBodyScanResult(BodyScanResponse result)
        {
            if (result?.BodyAnalysis == null)
                return false;

            return result.BodyAnalysis.BMI > 0 &&
                   result.BodyAnalysis.EstimatedBodyFatPercentage >= 0 &&
                   result.BodyAnalysis.EstimatedMusclePercentage >= 0;
        }

        private bool ValidateVoiceWorkoutResult(VoiceWorkoutResponse result)
        {
            if (result?.WorkoutData == null)
                return false;

            return !string.IsNullOrEmpty(result.WorkoutData.Type) &&
                   result.WorkoutData.StartTime != default;
        }

        private bool ValidateVoiceFoodResult(VoiceFoodResponse result)
        {
            if (result?.FoodItems == null || !result.FoodItems.Any())
                return false;

            return result.FoodItems.All(item =>
                !string.IsNullOrEmpty(item.Name) &&
                item.EstimatedWeight > 0);
        }
    }
}
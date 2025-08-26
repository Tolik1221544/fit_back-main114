using FitnessTracker.API.DTOs;

namespace FitnessTracker.API.Services.AI
{
    public interface IAIErrorHandlerService
    {
        FoodScanResponse CreateFallbackFoodResponse(string reason, string? locale = null, byte[]? imageData = null);
        VoiceWorkoutResponse CreateFallbackWorkoutResponse(string reason, string? locale = null, string? workoutType = null);
        VoiceFoodResponse CreateFallbackVoiceFoodResponse(string reason, string? locale = null, string? mealType = null);
        BodyScanResponse CreateFallbackBodyResponse(string reason, string? locale = null);
        TextWorkoutResponse CreateFallbackTextWorkoutResponse(string reason, string? locale = null, string? workoutType = null);
        TextFoodResponse CreateFallbackTextFoodResponse(string reason, string? locale = null, string? mealType = null);
        bool ShouldRetryRequest(Exception ex, int currentAttempt);
    }

    public class AIErrorHandlerService : IAIErrorHandlerService
    {
        private readonly ILogger<AIErrorHandlerService> _logger;

        public AIErrorHandlerService(ILogger<AIErrorHandlerService> logger)
        {
            _logger = logger;
        }

        private string GetMaintenanceMessage(string? locale)
        {
            var lang = GetLanguageFromLocale(locale ?? "en");

            return lang switch
            {
                "ru" => "Ведутся технические работы. Пожалуйста, попробуйте через 30 минут.",
                "es" => "Mantenimiento del servidor en curso. Por favor, inténtelo de nuevo en 30 minutos.",
                "de" => "Serverwartung im Gange. Bitte versuchen Sie es in 30 Minuten erneut.",
                "fr" => "Maintenance du serveur en cours. Veuillez réessayer dans 30 minutes.",
                "zh" => "服务器正在维护中。请30分钟后再试。",
                "ja" => "サーバーメンテナンス中です。30分後にもう一度お試しください。",
                "ko" => "서버 유지 보수 중입니다. 30분 후에 다시 시도해 주세요.",
                "pt" => "Manutenção do servidor em andamento. Por favor, tente novamente em 30 minutos.",
                "it" => "Manutenzione del server in corso. Si prega di riprovare tra 30 minuti.",
                "ar" => "الخادم قيد الصيانة. يرجى المحاولة مرة أخرى بعد 30 دقيقة.",
                "hi" => "सर्वर रखरखाव चल रहा है। कृपया 30 मिनट बाद पुनः प्रयास करें।",
                "tr" => "Sunucu bakımı devam ediyor. Lütfen 30 dakika sonra tekrar deneyin.",
                "pl" => "Trwa konserwacja serwera. Spróbuj ponownie za 30 minut.",
                "uk" => "Проводяться технічні роботи. Будь ласка, спробуйте через 30 хвилин.",
                _ => "Server maintenance in progress. Please try again in 30 minutes."
            };
        }

        private string GetLanguageFromLocale(string? locale)
        {
            if (string.IsNullOrEmpty(locale))
                return "en";

            var normalizedLocale = locale.Replace("-", "_").ToLower();
            var lang = normalizedLocale.Length >= 2 ? normalizedLocale.Substring(0, 2) : normalizedLocale;
            return lang;
        }

        public FoodScanResponse CreateFallbackFoodResponse(string reason, string? locale = null, byte[]? imageData = null)
        {
            _logger.LogError($"🍎 Food analysis failed: {reason}");

            return new FoodScanResponse
            {
                Success = false,
                ErrorMessage = GetMaintenanceMessage(locale),
                FoodItems = new List<FoodItemResponse>(),
                EstimatedCalories = 0,
                FullDescription = string.Empty
            };
        }

        public VoiceWorkoutResponse CreateFallbackWorkoutResponse(string reason, string? locale = null, string? workoutType = null)
        {
            _logger.LogError($"🎤 Workout analysis failed: {reason}");

            return new VoiceWorkoutResponse
            {
                Success = false,
                ErrorMessage = GetMaintenanceMessage(locale),
                TranscribedText = string.Empty,
                WorkoutData = null
            };
        }

        public VoiceFoodResponse CreateFallbackVoiceFoodResponse(string reason, string? locale = null, string? mealType = null)
        {
            _logger.LogError($"🗣️ Food voice analysis failed: {reason}");

            return new VoiceFoodResponse
            {
                Success = false,
                ErrorMessage = GetMaintenanceMessage(locale),
                TranscribedText = string.Empty,
                FoodItems = new List<FoodItemResponse>(),
                EstimatedTotalCalories = 0
            };
        }

        public BodyScanResponse CreateFallbackBodyResponse(string reason, string? locale = null)
        {
            _logger.LogError($"💪 Body analysis failed: {reason}");

            return new BodyScanResponse
            {
                Success = false,
                ErrorMessage = GetMaintenanceMessage(locale),
                BodyAnalysis = new BodyAnalysisDto
                {
                    EstimatedBodyFatPercentage = 0,
                    EstimatedMusclePercentage = 0,
                    BodyType = string.Empty,
                    PostureAnalysis = string.Empty,
                    OverallCondition = string.Empty,
                    BMI = 0,
                    BMICategory = string.Empty,
                    EstimatedWaistCircumference = 0,
                    EstimatedChestCircumference = 0,
                    EstimatedHipCircumference = 0,
                    BasalMetabolicRate = 0,
                    MetabolicRateCategory = string.Empty,
                    TrainingFocus = string.Empty,
                    ExerciseRecommendations = new List<string>(),
                    NutritionRecommendations = new List<string>()
                },
                Recommendations = new List<string>(),
                FullAnalysis = string.Empty
            };
        }

        public TextWorkoutResponse CreateFallbackTextWorkoutResponse(string reason, string? locale = null, string? workoutType = null)
        {
            _logger.LogError($"📝 Text workout analysis failed: {reason}");

            return new TextWorkoutResponse
            {
                Success = false,
                ErrorMessage = GetMaintenanceMessage(locale),
                ProcessedText = string.Empty,
                WorkoutData = null
            };
        }

        public TextFoodResponse CreateFallbackTextFoodResponse(string reason, string? locale = null, string? mealType = null)
        {
            _logger.LogError($"📝 Text food analysis failed: {reason}");

            return new TextFoodResponse
            {
                Success = false,
                ErrorMessage = GetMaintenanceMessage(locale),
                ProcessedText = string.Empty,
                FoodItems = new List<FoodItemResponse>(),
                EstimatedTotalCalories = 0
            };
        }

        public bool ShouldRetryRequest(Exception ex, int currentAttempt)
        {
            const int maxAttempts = 3;

            if (currentAttempt >= maxAttempts)
                return false;

            if (ex.Message.Contains("403") || ex.Message.Contains("401"))
            {
                _logger.LogError($"❌ Not retrying - authorization error: {ex.Message}");
                return false;
            }

            if (ex is HttpRequestException || ex is TaskCanceledException)
                return true;

            if (ex.Message.Contains("503") || ex.Message.Contains("502") || ex.Message.Contains("timeout"))
                return true;

            return false;
        }
    }
}
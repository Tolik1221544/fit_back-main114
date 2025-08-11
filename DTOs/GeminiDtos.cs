using System.Text.Json.Serialization;

namespace FitnessTracker.API.DTOs
{
    /// <summary>
    /// 🍎 Ответ на анализ еды
    /// </summary>
    public class FoodScanResponse
    {
        public bool Success { get; set; }
        public string? ErrorMessage { get; set; }
        public List<FoodItemResponse>? FoodItems { get; set; }
        public int EstimatedCalories { get; set; }
        public string? FullDescription { get; set; }
        public string? ImageUrl { get; set; } // НОВОЕ: URL сохраненного изображения
    }

    /// <summary>
    /// 🍎 Ответ с дополнительным URL изображения
    /// </summary>
    public class ScanFoodResponseWithImage
    {
        public List<FoodIntakeDto>? Items { get; set; }
        public string? ImageUrl { get; set; }
    }

    /// <summary>
    /// 🍎 Элемент еды в ответе
    /// </summary>
    public class FoodItemResponse
    {
        public string Name { get; set; } = string.Empty;
        public decimal EstimatedWeight { get; set; }
        public string WeightType { get; set; } = "g";
        public string? Description { get; set; }
        public NutritionPer100gDto NutritionPer100g { get; set; } = new NutritionPer100gDto();
        public int TotalCalories { get; set; }
        public decimal Confidence { get; set; }
    }

    /// <summary>
    /// 💪 Ответ на анализ тела
    /// </summary>
    public class BodyScanResponse
    {
        public bool Success { get; set; }
        public string? ErrorMessage { get; set; }
        public BodyAnalysisDto BodyAnalysis { get; set; } = new BodyAnalysisDto();
        public List<string> Recommendations { get; set; } = new List<string>();
        public string? FullAnalysis { get; set; }

        // НОВОЕ: URLs изображений
        public string? FrontImageUrl { get; set; }
        public string? SideImageUrl { get; set; }
        public string? BackImageUrl { get; set; }
    }

    /// <summary>
    /// 💪 Анализ тела
    /// </summary>
    public class BodyAnalysisDto
    {
        public decimal EstimatedBodyFatPercentage { get; set; }
        public decimal EstimatedMusclePercentage { get; set; }
        public string BodyType { get; set; } = string.Empty;
        public string PostureAnalysis { get; set; } = string.Empty;
        public string OverallCondition { get; set; } = string.Empty;
        public decimal BMI { get; set; }
        public string BMICategory { get; set; } = string.Empty;
        public decimal EstimatedWaistCircumference { get; set; }
        public decimal EstimatedChestCircumference { get; set; }
        public decimal EstimatedHipCircumference { get; set; }

        public int BasalMetabolicRate { get; set; } // Основной обмен в ккал (например: 1200)
        public string MetabolicRateCategory { get; set; } = string.Empty; // "Низкий", "Нормальный", "Высокий"

        public List<string> ExerciseRecommendations { get; set; } = new List<string>();
        public List<string> NutritionRecommendations { get; set; } = new List<string>();
        public string TrainingFocus { get; set; } = string.Empty;
    }

    /// <summary>
    /// 🎤 Ответ на голосовой ввод тренировки
    /// </summary>
    public class VoiceWorkoutResponse
    {
        public bool Success { get; set; }
        public string? ErrorMessage { get; set; }
        public string? TranscribedText { get; set; }
        public ActivityDto? WorkoutData { get; set; } 
    }

    /// <summary>
    /// 🗣️ Ответ на голосовой ввод еды
    /// </summary>
    public class VoiceFoodResponse
    {
        public bool Success { get; set; }
        public string? ErrorMessage { get; set; }
        public string? TranscribedText { get; set; }
        public List<FoodItemResponse>? FoodItems { get; set; }
        public int EstimatedTotalCalories { get; set; }
    }


    /// <summary>
    /// 📤 Запрос к Gemini API
    /// </summary>
    public class GeminiRequest
    {
        [JsonPropertyName("contents")]
        public List<GeminiContent> Contents { get; set; } = new List<GeminiContent>();

        [JsonPropertyName("generationConfig")]
        public GeminiGenerationConfig? GenerationConfig { get; set; }

        [JsonPropertyName("safetySettings")]
        public List<GeminiSafetySetting> SafetySettings { get; set; } = new List<GeminiSafetySetting>();
    }

    /// <summary>
    /// 📥 Ответ от Gemini API
    /// </summary>
    public class GeminiResponse
    {
        [JsonPropertyName("candidates")]
        public List<GeminiCandidate> Candidates { get; set; } = new List<GeminiCandidate>();

        [JsonPropertyName("usageMetadata")]
        public GeminiUsageMetadata? UsageMetadata { get; set; }
    }

    /// <summary>
    /// 📄 Контент для Gemini
    /// </summary>
    public class GeminiContent
    {
        [JsonPropertyName("parts")]
        public List<GeminiPart> Parts { get; set; } = new List<GeminiPart>();

        [JsonPropertyName("role")]
        public string? Role { get; set; }
    }

    /// <summary>
    /// 🧩 Часть контента (текст или изображение)
    /// </summary>
    public class GeminiPart
    {
        [JsonPropertyName("text")]
        public string? Text { get; set; }

        [JsonPropertyName("inlineData")]
        public GeminiInlineData? InlineData { get; set; }
    }

    /// <summary>
    /// 📎 Встроенные данные (изображения, аудио)
    /// </summary>
    public class GeminiInlineData
    {
        [JsonPropertyName("mimeType")]
        public string MimeType { get; set; } = string.Empty;

        [JsonPropertyName("data")]
        public string Data { get; set; } = string.Empty;
    }

    /// <summary>
    /// 🎯 Кандидат ответа
    /// </summary>
    public class GeminiCandidate
    {
        [JsonPropertyName("content")]
        public GeminiContent? Content { get; set; }

        [JsonPropertyName("finishReason")]
        public string? FinishReason { get; set; }

        [JsonPropertyName("index")]
        public int Index { get; set; }

        [JsonPropertyName("safetyRatings")]
        public List<GeminiSafetyRating> SafetyRatings { get; set; } = new List<GeminiSafetyRating>();
    }

    /// <summary>
    /// ⚙️ Конфигурация генерации
    /// </summary>
    public class GeminiGenerationConfig
    {
        [JsonPropertyName("temperature")]
        public double Temperature { get; set; } = 1.0;

        [JsonPropertyName("topK")]
        public int TopK { get; set; } = 1;

        [JsonPropertyName("topP")]
        public double TopP { get; set; } = 1.0;

        [JsonPropertyName("maxOutputTokens")]
        public int MaxOutputTokens { get; set; } = 2048;

        [JsonPropertyName("stopSequences")]
        public List<string> StopSequences { get; set; } = new List<string>();
    }

    /// <summary>
    /// 🛡️ Настройки безопасности
    /// </summary>
    public class GeminiSafetySetting
    {
        [JsonPropertyName("category")]
        public string Category { get; set; } = string.Empty;

        [JsonPropertyName("threshold")]
        public string Threshold { get; set; } = string.Empty;
    }

    /// <summary>
    /// 🛡️ Рейтинг безопасности
    /// </summary>
    public class GeminiSafetyRating
    {
        [JsonPropertyName("category")]
        public string Category { get; set; } = string.Empty;

        [JsonPropertyName("probability")]
        public string Probability { get; set; } = string.Empty;
    }

    /// <summary>
    /// 📊 Метаданные использования
    /// </summary>
    public class GeminiUsageMetadata
    {
        [JsonPropertyName("promptTokenCount")]
        public int PromptTokenCount { get; set; }

        [JsonPropertyName("candidatesTokenCount")]
        public int CandidatesTokenCount { get; set; }

        [JsonPropertyName("totalTokenCount")]
        public int TotalTokenCount { get; set; }
    }

    /// <summary>
    /// 📸 Запрос на анализ тела
    /// </summary>
    public class BodyScanRequest
    {
        public IFormFile? FrontImage { get; set; }
        public IFormFile? SideImage { get; set; }
        public IFormFile? BackImage { get; set; }
        public decimal? CurrentWeight { get; set; }
        public decimal? Height { get; set; }
        public int? Age { get; set; }
        public string? Gender { get; set; }
        public string? Goals { get; set; }
    }
}
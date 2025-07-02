namespace FitnessTracker.API.DTOs
{
    // DTO для сканирования еды
    public class FoodScanRequest
    {
        public IFormFile Image { get; set; } = null!;
        public string? UserPrompt { get; set; } // Дополнительные инструкции от пользователя
    }

    public class FoodScanResponse
    {
        public bool Success { get; set; }
        public string? ErrorMessage { get; set; }
        public List<FoodItemResponse> FoodItems { get; set; } = new List<FoodItemResponse>();
        public string? FullDescription { get; set; } // Полное описание от ИИ
        public int EstimatedCalories { get; set; } // Общие калории
    }

    public class FoodItemResponse
    {
        public string Name { get; set; } = string.Empty;
        public decimal EstimatedWeight { get; set; } // В граммах
        public string WeightType { get; set; } = "g";
        public string? Description { get; set; }
        public NutritionPer100gDto NutritionPer100g { get; set; } = new NutritionPer100gDto();
        public decimal TotalCalories { get; set; } // Калории с учетом веса
        public decimal Confidence { get; set; } // Уверенность ИИ (0-1)
    }

    // DTO для анализа тела
    public class BodyScanRequest
    {
        public IFormFile? FrontImage { get; set; }
        public IFormFile? SideImage { get; set; }
        public IFormFile? BackImage { get; set; }
        public decimal? CurrentWeight { get; set; }
        public decimal? Height { get; set; }
        public int? Age { get; set; }
        public string? Gender { get; set; }
        public string? Goals { get; set; } // Цели пользователя
    }

    public class BodyScanResponse
    {
        public bool Success { get; set; }
        public string? ErrorMessage { get; set; }
        public BodyAnalysisResult BodyAnalysis { get; set; } = new BodyAnalysisResult();
        public List<string> Recommendations { get; set; } = new List<string>();
        public string? FullAnalysis { get; set; } // Полный анализ от ИИ
    }

    public class BodyAnalysisResult
    {
        public decimal? EstimatedBodyFatPercentage { get; set; }
        public decimal? EstimatedMusclePercentage { get; set; }
        public string? BodyType { get; set; } // Эктоморф, мезоморф, эндоморф
        public string? PostureAnalysis { get; set; }
        public string? OverallCondition { get; set; }
        public decimal? BMI { get; set; }
        public string? BMICategory { get; set; }

        // Измерения (примерные)
        public decimal? EstimatedWaistCircumference { get; set; }
        public decimal? EstimatedChestCircumference { get; set; }
        public decimal? EstimatedHipCircumference { get; set; }

        // Рекомендации
        public List<string> ExerciseRecommendations { get; set; } = new List<string>();
        public List<string> NutritionRecommendations { get; set; } = new List<string>();
        public string? TrainingFocus { get; set; }
    }

    // DTO для голосового ввода тренировок
    public class VoiceWorkoutRequest
    {
        public IFormFile AudioFile { get; set; } = null!;
        public string? WorkoutType { get; set; } // "strength" или "cardio"
        public DateTime? WorkoutDate { get; set; }
    }

    public class VoiceWorkoutResponse
    {
        public bool Success { get; set; }
        public string? ErrorMessage { get; set; }
        public string? TranscribedText { get; set; } // Расшифрованный текст
        public WorkoutDataResponse WorkoutData { get; set; } = new WorkoutDataResponse();
    }

    public class WorkoutDataResponse
    {
        public string Type { get; set; } = string.Empty; // "strength" или "cardio"
        public DateTime StartTime { get; set; }
        public DateTime? EndTime { get; set; }
        public int? EstimatedCalories { get; set; }
        public StrengthDataDto? StrengthData { get; set; }
        public CardioDataDto? CardioData { get; set; }
        public List<string> Notes { get; set; } = new List<string>();
    }

    // DTO для голосового ввода еды
    public class VoiceFoodRequest
    {
        public IFormFile AudioFile { get; set; } = null!;
        public DateTime? MealTime { get; set; }
        public string? MealType { get; set; } // "breakfast", "lunch", "dinner", "snack"
    }

    public class VoiceFoodResponse
    {
        public bool Success { get; set; }
        public string? ErrorMessage { get; set; }
        public string? TranscribedText { get; set; }
        public List<FoodItemResponse> FoodItems { get; set; } = new List<FoodItemResponse>();
        public int EstimatedTotalCalories { get; set; }
    }

    // Общие настройки для ИИ
    public class AISettings
    {
        public string Language { get; set; } = "ru"; // Язык ответов
        public bool DetailedAnalysis { get; set; } = true;
        public decimal ConfidenceThreshold { get; set; } = 0.7m; // Минимальная уверенность
    }

    // Ответ от Gemini API (внутренний)
    public class GeminiResponse
    {
        public List<GeminiCandidate> Candidates { get; set; } = new List<GeminiCandidate>();
        public GeminiUsageMetadata? UsageMetadata { get; set; }
    }

    public class GeminiCandidate
    {
        public GeminiContent Content { get; set; } = new GeminiContent();
        public string? FinishReason { get; set; }
        public int Index { get; set; }
        public List<GeminiSafetyRating>? SafetyRatings { get; set; }
    }

    public class GeminiContent
    {
        public List<GeminiPart> Parts { get; set; } = new List<GeminiPart>();
        public string? Role { get; set; }
    }

    public class GeminiPart
    {
        public string? Text { get; set; }
        public GeminiInlineData? InlineData { get; set; }
    }

    public class GeminiInlineData
    {
        public string MimeType { get; set; } = string.Empty;
        public string Data { get; set; } = string.Empty; // Base64
    }

    public class GeminiSafetyRating
    {
        public string? Category { get; set; }
        public string? Probability { get; set; }
    }

    public class GeminiUsageMetadata
    {
        public int PromptTokenCount { get; set; }
        public int CandidatesTokenCount { get; set; }
        public int TotalTokenCount { get; set; }
    }

    public class GeminiRequest
    {
        public List<GeminiContent> Contents { get; set; } = new List<GeminiContent>();
        public GeminiGenerationConfig? GenerationConfig { get; set; }
        public List<GeminiSafetySetting>? SafetySettings { get; set; }
    }

    public class GeminiGenerationConfig
    {
        public int? Temperature { get; set; }
        public int? TopK { get; set; }
        public decimal? TopP { get; set; }
        public int? MaxOutputTokens { get; set; }
        public List<string>? StopSequences { get; set; }
    }

    public class GeminiSafetySetting
    {
        public string Category { get; set; } = string.Empty;
        public string Threshold { get; set; } = string.Empty;
    }
}
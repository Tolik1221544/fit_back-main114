using FitnessTracker.API.DTOs;

namespace FitnessTracker.API.DTOs
{
    public class TextWorkoutRequest
    {
        public string WorkoutDescription { get; set; } = string.Empty;
        public string? WorkoutType { get; set; }
        public bool SaveResults { get; set; } = false;
    }

    public class TextWorkoutResponse
    {
        public bool Success { get; set; }
        public string? ErrorMessage { get; set; }
        public string ProcessedText { get; set; } = string.Empty;
        public ActivityDto? WorkoutData { get; set; }
    }

    public class VoiceWorkoutResponse
    {
        public bool Success { get; set; }
        public string? ErrorMessage { get; set; }
        public string? TranscribedText { get; set; }
        public ActivityDto? WorkoutData { get; set; }
    }

    public class VoiceWorkoutResponseWithAudio : VoiceWorkoutResponse
    {
        public string? AudioUrl { get; set; }
        public string? AudioFileId { get; set; }
        public bool AudioSaved { get; set; }
        public double AudioSizeMB { get; set; }
    }

    public class VoiceFoodResponseWithAudio : VoiceFoodResponse
    {
        public string? AudioUrl { get; set; }
        public string? AudioFileId { get; set; }
        public bool AudioSaved { get; set; }
        public double AudioSizeMB { get; set; }
    }

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

    public class TextFoodRequest
    {
        public string FoodDescription { get; set; } = string.Empty;
        public string? MealType { get; set; }
        public bool SaveResults { get; set; } = false;
    }

    public class FoodCorrectionRequest
    {
        public string CorrectionText { get; set; } = string.Empty;
        public FoodItemRequest? FoodItem { get; set; }
        public bool SaveResults { get; set; } = false;
    }

    public class VoiceFoodResponse
    {
        public bool Success { get; set; }
        public string? ErrorMessage { get; set; }
        public string? TranscribedText { get; set; }
        public List<FoodItemResponse>? FoodItems { get; set; }
        public int EstimatedTotalCalories { get; set; }
    }

    public class FoodScanResponse
    {
        public bool Success { get; set; }
        public string? ErrorMessage { get; set; }
        public List<FoodItemResponse>? FoodItems { get; set; }
        public int EstimatedCalories { get; set; }
        public string? FullDescription { get; set; }
        public string? ImageUrl { get; set; }
    }

    public class BodyScanResponse
    {
        public bool Success { get; set; }
        public string? ErrorMessage { get; set; }
        public BodyAnalysisDto BodyAnalysis { get; set; } = new();
        public List<string> Recommendations { get; set; } = new();
        public string? FullAnalysis { get; set; }
        public string? FrontImageUrl { get; set; }
        public string? SideImageUrl { get; set; }
        public string? BackImageUrl { get; set; }
    }

    public class TextFoodResponse
    {
        public bool Success { get; set; }
        public string? ErrorMessage { get; set; }
        public string ProcessedText { get; set; } = string.Empty;
        public List<FoodItemResponse>? FoodItems { get; set; }
        public int EstimatedTotalCalories { get; set; }
    }

    public class FoodCorrectionResponse
    {
        public bool Success { get; set; }
        public string? ErrorMessage { get; set; }
        public FoodItemResponse CorrectedFoodItem { get; set; } = new();
        public string? CorrectionExplanation { get; set; }
        public List<string> Ingredients { get; set; } = new();
    }

    public class FoodItemResponse
    {
        public string Name { get; set; } = string.Empty;
        public decimal EstimatedWeight { get; set; }
        public string WeightType { get; set; } = "g";
        public string? Description { get; set; }
        public NutritionPer100gDto NutritionPer100g { get; set; } = new();
        public int TotalCalories { get; set; }
        public decimal Confidence { get; set; }
    }

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
        public int BasalMetabolicRate { get; set; }
        public string MetabolicRateCategory { get; set; } = string.Empty;
        public List<string> ExerciseRecommendations { get; set; } = new();
        public List<string> NutritionRecommendations { get; set; } = new();
        public string TrainingFocus { get; set; } = string.Empty;
    }

    public class GeminiResponse
    {
        public bool Success { get; set; }
        public string? Content { get; set; }
        public string? ErrorMessage { get; set; }
    }

    public class GeminiContent
    {
        public string Role { get; set; } = "user";
        public List<GeminiPart> Parts { get; set; } = new();
    }

    public class GeminiPart
    {
        public string? Text { get; set; }
        public GeminiInlineData? InlineData { get; set; }
    }

    public class GeminiInlineData
    {
        public string MimeType { get; set; } = string.Empty;
        public string Data { get; set; } = string.Empty;
    }

    public class GeminiGenerationConfig
    {
        public double Temperature { get; set; } = 0.1;
        public double TopP { get; set; } = 1.0;
        public int CandidateCount { get; set; } = 1;
        public int MaxOutputTokens { get; set; } = 2048;
    }

    public class ScanFoodResponseWithImage : ScanFoodResponse
    {
        public string? ImageUrl { get; set; }
    }

    public class FoodItemRequest
    {
        public string? TempItemId { get; set; }
        public string Name { get; set; } = string.Empty;
        public decimal Weight { get; set; }
        public string WeightType { get; set; } = "g";
        public string? Image { get; set; }
        public NutritionPer100gDto NutritionPer100g { get; set; } = new();
    }

    public class NutritionPer100gDto
    {
        public decimal Calories { get; set; }
        public decimal Proteins { get; set; }
        public decimal Fats { get; set; }
        public decimal Carbs { get; set; }
    }

    public class ScanFoodResponse
    {
        public List<FoodIntakeDto>? Items { get; set; }
        public bool Success { get; set; } = true;
        public string? ErrorMessage { get; set; }
    }
}
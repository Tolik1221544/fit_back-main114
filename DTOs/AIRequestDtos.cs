namespace FitnessTracker.API.DTOs
{
    public class TextWorkoutResponse
    {
        public bool Success { get; set; }
        public string? ErrorMessage { get; set; }
        public string ProcessedText { get; set; } = string.Empty;
        public ActivityDto? WorkoutData { get; set; }
    }

    public class TextFoodRequest
    {
        public string FoodDescription { get; set; } = string.Empty;
        public string? MealType { get; set; }
        public bool SaveResults { get; set; } = false;
    }

    public class FoodCorrectionRequest
    {
        public FoodItemResponse FoodItem { get; set; } = new();
        public string CorrectionText { get; set; } = string.Empty;
        public bool SaveResults { get; set; } = false;
    }

    public class TextWorkoutResponse
    {
        public bool Success { get; set; }
        public string? ErrorMessage { get; set; }
        public string ProcessedText { get; set; } = string.Empty;
        public WorkoutDataResponse? WorkoutData { get; set; }
    }

    public class TextFoodResponse
    {
        public bool Success { get; set; }
        public string? ErrorMessage { get; set; }
        public string ProcessedText { get; set; } = string.Empty;
        public List<FoodItemResponse> FoodItems { get; set; } = new List<FoodItemResponse>();
        public int EstimatedTotalCalories { get; set; }
    }

    public class FoodCorrectionResponse
    {
        public bool Success { get; set; }
        public string? ErrorMessage { get; set; }
        public FoodItemResponse CorrectedFoodItem { get; set; } = new FoodItemResponse();
        public string CorrectionExplanation { get; set; } = string.Empty;
        public List<string> Ingredients { get; set; } = new List<string>();
    }
}
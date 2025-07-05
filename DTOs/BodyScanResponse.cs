namespace FitnessTracker.API.DTOs
{
    public class BodyScanResponse
    {
        public bool Success { get; set; }
        public string? ErrorMessage { get; set; }
        public BodyAnalysisData BodyAnalysis { get; set; } = new BodyAnalysisData();
        public List<string> Recommendations { get; set; } = new List<string>();
        public string? FullAnalysis { get; set; }

        // НОВОЕ: URLs сохраненных изображений
        public string? FrontImageUrl { get; set; }
        public string? SideImageUrl { get; set; }
        public string? BackImageUrl { get; set; }
    }

    public class BodyAnalysisData
    {
        public decimal EstimatedBodyFatPercentage { get; set; }
        public decimal EstimatedMusclePercentage { get; set; }
        public string BodyType { get; set; } = string.Empty;
        public string PostureAnalysis { get; set; } = string.Empty;
        public string OverallCondition { get; set; } = string.Empty;
        public decimal BMI { get; set; }
        public string BMICategory { get; set; } = string.Empty;
        public decimal? EstimatedWaistCircumference { get; set; }
        public decimal? EstimatedChestCircumference { get; set; }
        public decimal? EstimatedHipCircumference { get; set; }
        public List<string> ExerciseRecommendations { get; set; } = new List<string>();
        public List<string> NutritionRecommendations { get; set; } = new List<string>();
        public string TrainingFocus { get; set; } = string.Empty;
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
}
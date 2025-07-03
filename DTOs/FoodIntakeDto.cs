namespace FitnessTracker.API.DTOs
{
    public class AddFoodIntakeRequest
    {
        public List<FoodItemRequest> Items { get; set; } = new List<FoodItemRequest>();
        public DateTime DateTime { get; set; } = DateTime.UtcNow;
    }

    public class FoodItemRequest
    {
        public string? TempItemId { get; set; }
        public string Name { get; set; } = string.Empty;
        public decimal Weight { get; set; }
        public string WeightType { get; set; } = "g";
        public string? Image { get; set; }
        public NutritionPer100gDto NutritionPer100g { get; set; } = new NutritionPer100gDto();
    }

    public class FoodItemAnalysis
    {
        public string Name { get; set; } = string.Empty;
        public decimal EstimatedWeight { get; set; }
        public string WeightType { get; set; } = "g";
        public string? Description { get; set; }
        public NutritionPer100gDto NutritionPer100g { get; set; } = new NutritionPer100gDto();
        public int TotalCalories { get; set; }
        public decimal Confidence { get; set; }
    }

    public class UpdateFoodIntakeRequest
    {
        public string Name { get; set; } = string.Empty;
        public decimal Weight { get; set; }
        public string WeightType { get; set; } = "g";
        public NutritionPer100gDto NutritionPer100g { get; set; } = new NutritionPer100gDto();
    }

    public class FoodIntakeDto
    {
        public string Id { get; set; } = string.Empty;
        public string? TempItemId { get; set; }
        public string Name { get; set; } = string.Empty;
        public decimal Weight { get; set; }
        public string WeightType { get; set; } = "g";
        public string? Image { get; set; }
        public DateTime DateTime { get; set; }
        public NutritionPer100gDto NutritionPer100g { get; set; } = new NutritionPer100gDto();
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
        public List<FoodIntakeDto> Items { get; set; } = new List<FoodIntakeDto>();
    }
}
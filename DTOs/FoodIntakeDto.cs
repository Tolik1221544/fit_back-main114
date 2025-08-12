namespace FitnessTracker.API.DTOs
{
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

    public class AddFoodIntakeRequest
    {
        public List<FoodItemRequest> Items { get; set; } = new List<FoodItemRequest>();
        public DateTime DateTime { get; set; } = DateTime.UtcNow;
    }

    public class UpdateFoodIntakeRequest
    {
        public string Name { get; set; } = string.Empty;
        public decimal Weight { get; set; }
        public string WeightType { get; set; } = "g";
        public string? Image { get; set; }
        public DateTime DateTime { get; set; }
        public NutritionPer100gDto NutritionPer100g { get; set; } = new NutritionPer100gDto();
    }
}

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

    public class NutritionPer100gDto
    {
        public decimal Calories { get; set; }
        public decimal Proteins { get; set; }
        public decimal Fats { get; set; }
        public decimal Carbs { get; set; }
    }

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

    public class UpdateFoodIntakeRequest
    {
        public string Name { get; set; } = string.Empty;
        public decimal Weight { get; set; }
        public string WeightType { get; set; } = "g";
        public string? Image { get; set; }
        public DateTime DateTime { get; set; }
        public NutritionPer100gDto NutritionPer100g { get; set; } = new NutritionPer100gDto();
    }

    /// <summary>
    /// 📸 Ответ на сканирование еды (legacy)
    /// </summary>
    public class ScanFoodResponse
    {
        public List<FoodIntakeDto>? Items { get; set; }
        public bool Success { get; set; } = true;
        public string? ErrorMessage { get; set; }
    }
}
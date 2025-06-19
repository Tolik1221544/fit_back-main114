namespace FitnessTracker.API.Models
{
    public class FoodIntake
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string UserId { get; set; } = string.Empty;
        public string TempItemId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public decimal Weight { get; set; }
        public DateTime DateTime { get; set; }
        public NutritionPer100g NutritionPer100g { get; set; } = new NutritionPer100g();

        // Navigation property
        public User User { get; set; } = null!;
    }

    public class NutritionPer100g
    {
        public decimal Calories { get; set; }
        public decimal Proteins { get; set; }
        public decimal Fats { get; set; }
        public decimal Carbs { get; set; }
    }
}

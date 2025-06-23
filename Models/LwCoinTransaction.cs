namespace FitnessTracker.API.Models
{
    public class LwCoinTransaction
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string UserId { get; set; } = string.Empty;
        public int Amount { get; set; } // ѕоложительное = получение, отрицательное = трата
        public string Type { get; set; } = string.Empty; // "photo", "voice", "text", "refill", "purchase", "referral"
        public string Description { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public string FeatureUsed { get; set; } = string.Empty; // "food_scan", "activity_log", etc.

        // Navigation property
        public User User { get; set; } = null!;
    }
}
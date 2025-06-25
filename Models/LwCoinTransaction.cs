namespace FitnessTracker.API.Models
{
    public class LwCoinTransaction
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string UserId { get; set; } = string.Empty;
        public int Amount { get; set; }
        public string Type { get; set; } = string.Empty; // "earned", "spent", "refill", "purchase", "referral"
        public string Description { get; set; } = string.Empty;
        public string FeatureUsed { get; set; } = string.Empty; // "photo", "voice", "text", "exercise", "archive"
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public string? ReferralId { get; set; } // If this transaction is related to a referral

        // Navigation property
        public User User { get; set; } = null!;
    }
}
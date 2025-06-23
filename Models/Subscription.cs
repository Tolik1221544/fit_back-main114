namespace FitnessTracker.API.Models
{
    public class Subscription
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string UserId { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty; // "premium", "coin_pack_50", "coin_pack_100"
        public decimal Price { get; set; }
        public string Currency { get; set; } = "USD";
        public DateTime PurchasedAt { get; set; } = DateTime.UtcNow;
        public DateTime? ExpiresAt { get; set; }
        public bool IsActive { get; set; } = true;
        public string PaymentTransactionId { get; set; } = string.Empty;

        // Navigation property
        public User User { get; set; } = null!;
    }
}
namespace FitnessTracker.API.Models
{
    public class PurchaseVerification
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string UserId { get; set; } = string.Empty;

        public string Platform { get; set; } = string.Empty; 

        public string PurchaseToken { get; set; } = string.Empty; 
        public string TransactionId { get; set; } = string.Empty; 

        public string ProductId { get; set; } = string.Empty;
        public string PackageType { get; set; } = string.Empty; 

        public string VerificationStatus { get; set; } = "pending"; 
        public DateTime? VerifiedAt { get; set; }
        public DateTime? ExpiresAt { get; set; }

        public decimal Price { get; set; }
        public int CoinsAmount { get; set; }
        public int DurationDays { get; set; }

        public bool IsRestored { get; set; } = false;
        public DateTime? RestoredAt { get; set; }

        public string? VerificationError { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        public User User { get; set; } = null!;
    }
}

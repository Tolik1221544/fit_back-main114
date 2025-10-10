namespace FitnessTracker.API.Models
{
    public class PendingPayment
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string PaymentId { get; set; } = ""; 
        public long TelegramId { get; set; }
        public string UserId { get; set; } = "";
        public decimal Amount { get; set; }
        public string Currency { get; set; } = "EUR";
        public string PackageId { get; set; } = ""; 

        public int CoinsAmount { get; set; }
        public int DurationDays { get; set; }

        public string Status { get; set; } = "pending"; 
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? CompletedAt { get; set; }

        public User User { get; set; } = null!;
    }
}
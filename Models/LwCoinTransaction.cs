namespace FitnessTracker.API.Models
{
    public class LwCoinTransaction
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string UserId { get; set; } = string.Empty;
        public int Amount { get; set; }
        public double FractionalAmount { get; set; } = 0.0;
        public string Type { get; set; } = string.Empty; 
        public string Description { get; set; } = string.Empty;
        public string FeatureUsed { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public string? ReferralId { get; set; }
        public decimal? Price { get; set; }
        public string? Period { get; set; } = string.Empty;
        public string UsageDate { get; set; } = DateTime.UtcNow.Date.ToString("yyyy-MM-dd");

        public string CoinSource { get; set; } = "permanent"; 
        public DateTime? ExpiryDate { get; set; } 
        public bool IsExpired { get; set; } = false;
        public string? SubscriptionId { get; set; } 

        public User User { get; set; } = null!;
    }

    public class UserCoinBalance
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string UserId { get; set; } = string.Empty;

        public decimal SubscriptionCoins { get; set; } = 0; 
        public decimal ReferralCoins { get; set; } = 0;     
        public decimal BonusCoins { get; set; } = 0;       
        public decimal PermanentCoins { get; set; } = 0;    
        public decimal RegistrationCoins { get; set; } = 0; 

        public decimal TotalCoins => SubscriptionCoins + ReferralCoins + BonusCoins + PermanentCoins + RegistrationCoins;
        public decimal PermanentTotal => ReferralCoins + BonusCoins + PermanentCoins + RegistrationCoins;

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        public User User { get; set; } = null!;
    }
}
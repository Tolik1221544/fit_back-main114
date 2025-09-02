namespace FitnessTracker.API.DTOs
{
    public class LwCoinBalanceDto
    {
        public int Balance { get; set; }
        public bool IsPremium { get; set; }
        public DateTime? PremiumExpiresAt { get; set; }
        public DateTime NextRefillDate { get; set; }
        public PremiumNotificationDto? PremiumNotification { get; set; }

        public object? DetailedBalance { get; set; } 

        public int MonthlyAllowance { get; set; }
        public int UsedThisMonth { get; set; }
        public int RemainingThisMonth { get; set; }
        public decimal DailyUsage { get; set; }
        public decimal DailyLimit { get; set; }
        public decimal DailyRemaining { get; set; }
    }

    public class SpendLwCoinsRequest
    {
        public int Amount { get; set; }
        public string Type { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string FeatureUsed { get; set; } = string.Empty;
        public decimal? Price { get; set; }
        public string? Period { get; set; }
    }

    public class LwCoinTransactionDto
    {
        public string Id { get; set; } = string.Empty;
        public int Amount { get; set; }
        public decimal FractionalAmount { get; set; }
        public string Type { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string FeatureUsed { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public decimal? Price { get; set; }
        public string? Period { get; set; }
        public string UsageDate { get; set; } = string.Empty;

        // ✅ НОВЫЕ ПОЛЯ
        public string CoinSource { get; set; } = "permanent";
        public DateTime? ExpiryDate { get; set; }
        public bool IsExpired { get; set; } = false;
    }

    public class PurchasePremiumRequest
    {
        public string PaymentTransactionId { get; set; } = string.Empty;
        public decimal Price { get; set; } = 8.99m;
        public string Period { get; set; } = "monthly";
    }

    public class PurchaseCoinPackRequest
    {
        public string PackType { get; set; } = string.Empty;
        public string PaymentTransactionId { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public string Period { get; set; } = "one-time";
    }

    public class LwCoinLimitsDto
    {
        public int MonthlyAllowance { get; set; }
        public int UsedThisMonth { get; set; }
        public int RemainingThisMonth { get; set; }
        public bool IsPremium { get; set; }
        public Dictionary<string, int> FeatureUsage { get; set; } = new Dictionary<string, int>();
        public decimal DailyUsage { get; set; }
        public decimal DailyLimit { get; set; }
        public decimal DailyRemaining { get; set; }

        public object? DetailedBalance { get; set; }
    }

    public class PremiumNotificationDto
    {
        public string Type { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public DateTime? ExpiresAt { get; set; }
        public int DaysRemaining { get; set; }
        public bool IsUrgent { get; set; }
    }
}
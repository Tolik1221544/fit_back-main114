namespace FitnessTracker.API.DTOs
{
    public class LwCoinBalanceDto
    {
        public int Balance { get; set; }
        public int MonthlyAllowance { get; set; }
        public int UsedThisMonth { get; set; }
        public int RemainingThisMonth { get; set; }
        public bool IsPremium { get; set; }
        public DateTime? PremiumExpiresAt { get; set; }
        public DateTime NextRefillDate { get; set; }
    }

    public class SpendLwCoinsRequest
    {
        public int Amount { get; set; }
        public string Type { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string FeatureUsed { get; set; } = string.Empty;
    }

    public class LwCoinTransactionDto
    {
        public string Id { get; set; } = string.Empty;
        public int Amount { get; set; }
        public string Type { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string FeatureUsed { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
    }

    public class PurchasePremiumRequest
    {
        public string PaymentTransactionId { get; set; } = string.Empty;
    }

    public class PurchaseCoinPackRequest
    {
        public string PackType { get; set; } = string.Empty; // "pack_50", "pack_100"
        public string PaymentTransactionId { get; set; } = string.Empty;
    }

    public class LwCoinLimitsDto
    {
        public int MonthlyAllowance { get; set; }
        public int UsedThisMonth { get; set; }
        public int RemainingThisMonth { get; set; }
        public bool IsPremium { get; set; }
        public Dictionary<string, int> FeatureUsage { get; set; } = new Dictionary<string, int>();
    }
}
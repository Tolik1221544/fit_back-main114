namespace FitnessTracker.API.DTOs
{
    public class LwCoinBalanceDto
    {
        public int Balance { get; set; }
        public int MonthlyUsed { get; set; }
        public int MonthlyLimit { get; set; } // -1 = безлимит
        public bool HasPremium { get; set; }
        public DateTime? PremiumExpiresAt { get; set; }
        public int DaysUntilRefill { get; set; }
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
        public string PaymentMethod { get; set; } = string.Empty;
    }

    public class PurchaseCoinPackRequest
    {
        public string PackType { get; set; } = string.Empty; // "pack_50", "pack_100"
        public string PaymentTransactionId { get; set; } = string.Empty;
    }

    public class SpendLwCoinsRequest
    {
        public int Amount { get; set; }
        public string Type { get; set; } = string.Empty; // "photo", "voice", "text"
        public string Description { get; set; } = string.Empty;
        public string FeatureUsed { get; set; } = string.Empty;
    }

    public class LwCoinLimitsDto
    {
        public bool CanUseFeature { get; set; }
        public int CostPerUse { get; set; }
        public int RemainingCoins { get; set; }
        public bool HasPremium { get; set; }
        public string LimitType { get; set; } = string.Empty; // "monthly", "unlimited"
    }
}
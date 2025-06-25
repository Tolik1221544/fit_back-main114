namespace FitnessTracker.API.DTOs
{
    public class LwCoinBalanceDto
    {
        public int Balance { get; set; }
        public int MonthlyAllowance { get; set; } = 300;
        public int UsedThisMonth { get; set; }
        public int RemainingThisMonth { get; set; }
        public bool IsPremium { get; set; }
        public DateTime? PremiumExpiresAt { get; set; }
        public DateTime NextRefillDate { get; set; }
    }

    public class LwCoinTransactionDto
    {
        public string Id { get; set; } = string.Empty;
        public int Amount { get; set; }
        public string Type { get; set; } = string.Empty; // "earned", "spent", "refill", "purchase"
        public string Description { get; set; } = string.Empty;
        public string FeatureUsed { get; set; } = string.Empty; // "photo", "voice", "text", "exercise", "archive"
        public string SpentOn { get; set; } = string.Empty; // "food_scan", "voice_note", "text_analysis"
        public DateTime CreatedAt { get; set; }
    }

    // Новый DTO для системы опыта
    public class ExperienceTransactionDto
    {
        public string Id { get; set; } = string.Empty;
        public int Experience { get; set; }
        public string Source { get; set; } = string.Empty; // "mission", "daily_goal", "achievement"
        public string Description { get; set; } = string.Empty;
        public int LevelBefore { get; set; }
        public int LevelAfter { get; set; }
        public bool LeveledUp { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class SpendLwCoinsRequest
    {
        public int Amount { get; set; }
        public string Type { get; set; } = string.Empty; // "photo", "voice", "text"
        public string Description { get; set; } = string.Empty;
        public string FeatureUsed { get; set; } = string.Empty;
    }

    public class PurchasePremiumRequest
    {
        public string PaymentMethod { get; set; } = string.Empty;
        public string PaymentTransactionId { get; set; } = string.Empty;
    }

    public class PurchaseCoinPackRequest
    {
        public string PackType { get; set; } = string.Empty; // "pack_50", "pack_100"
        public string PaymentMethod { get; set; } = string.Empty;
        public string PaymentTransactionId { get; set; } = string.Empty;
    }

    public class LwCoinLimitsDto
    {
        public int MonthlyAllowance { get; set; } = 300;
        public int UsedThisMonth { get; set; }
        public int RemainingThisMonth { get; set; }
        public bool IsPremium { get; set; }
        public Dictionary<string, int> FeatureCosts { get; set; } = new Dictionary<string, int>
        {
            ["photo"] = 1,
            ["voice"] = 1,
            ["text"] = 1,
            ["exercise"] = 0,
            ["archive"] = 0
        };
        public Dictionary<string, int> FeatureUsage { get; set; } = new Dictionary<string, int>();
    }
}
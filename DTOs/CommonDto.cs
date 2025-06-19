namespace FitnessTracker.API.DTOs
{
    public class MissionDto
    {
        public string Id { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public int RewardCoins { get; set; }
        public string Type { get; set; } = string.Empty;
        public int TargetValue { get; set; }
        public int Progress { get; set; }
        public bool IsCompleted { get; set; }
    }

    public class AchievementDto
    {
        public string Id { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string IconUrl { get; set; } = string.Empty;
        public DateTime UnlockedAt { get; set; }
    }

    public class CoinBalanceDto
    {
        public int Balance { get; set; }
    }

    public class CoinTransactionDto
    {
        public string Id { get; set; } = string.Empty;
        public int Amount { get; set; }
        public string Type { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
    }

    public class PurchaseCoinRequest
    {
        public int Amount { get; set; }
        public string PaymentMethod { get; set; } = string.Empty;
    }

    public class SkinDto
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public int Cost { get; set; }
        public string ImageUrl { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public bool IsOwned { get; set; }
    }

    public class PurchaseSkinRequest
    {
        public string SkinId { get; set; } = string.Empty;
    }

    public class SetReferralRequest
    {
        public string ReferralCode { get; set; } = string.Empty;
    }
}

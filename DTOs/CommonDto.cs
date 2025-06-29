namespace FitnessTracker.API.DTOs
{
    public class MissionDto
    {
        public string Id { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Icon { get; set; } = string.Empty;
        public int RewardExperience { get; set; }
        public string Type { get; set; } = string.Empty;
        public int TargetValue { get; set; }
        public int Progress { get; set; }
        public bool IsCompleted { get; set; }
        public DateTime? CompletedAt { get; set; }

        public string? Route { get; set; } = null;
    }

    public class AchievementDto
    {
        public string Id { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Icon { get; set; } = string.Empty;
        public string ImageUrl { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public int RequiredValue { get; set; }
        public int CurrentProgress { get; set; }
        public bool IsUnlocked { get; set; }
        public DateTime? UnlockedAt { get; set; }
        public int ProgressPercentage => RequiredValue > 0 ? (CurrentProgress * 100 / RequiredValue) : 0;
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


        public decimal ExperienceBoost { get; set; } = 1.0m;
        public int Tier { get; set; } = 1;
        public bool IsActive { get; set; } = false; // ������� �� ���� � ������������
    }

    public class PurchaseSkinRequest
    {
        public string SkinId { get; set; } = string.Empty;
    }

    public class ActivateSkinRequest
    {
        public string SkinId { get; set; } = string.Empty;
    }

    public class SetReferralRequest
    {
        public string ReferralCode { get; set; } = string.Empty;
    }
}
namespace FitnessTracker.API.DTOs
{
    /// <summary>
    /// 🎯 DTO для миссий
    /// </summary>
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
        public string? Route { get; set; }
    }

    /// <summary>
    /// 🏆 DTO для достижений
    /// </summary>
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
    }

    /// <summary>
    /// 🎨 DTO для скинов
    /// </summary>
    public class SkinDto
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public int Cost { get; set; }
        public string ImageUrl { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public decimal ExperienceBoost { get; set; } = 1.0m;
        public int Tier { get; set; } = 1;
        public bool IsOwned { get; set; }
        public bool IsActive { get; set; }
    }

    /// <summary>
    /// 💰 Запрос на покупку скина
    /// </summary>
    public class PurchaseSkinRequest
    {
        public string SkinId { get; set; } = string.Empty;
    }

    /// <summary>
    /// ⚡ Запрос на активацию скина
    /// </summary>
    public class ActivateSkinRequest
    {
        public string SkinId { get; set; } = string.Empty;
    }

    /// <summary>
    /// 🤝 Запрос на установку реферала
    /// </summary>
    public class SetReferralRequest
    {
        public string ReferralCode { get; set; } = string.Empty;
    }
}
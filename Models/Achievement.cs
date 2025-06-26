namespace FitnessTracker.API.Models
{
    public class Mission
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Title { get; set; } = string.Empty;
        public string Icon { get; set; } = string.Empty;
        public int RewardExperience { get; set; }
        public string Type { get; set; } = string.Empty; // "activity", "food_intake", etc.
        public int TargetValue { get; set; }
        public bool IsActive { get; set; } = true;

        // Navigation properties
        public ICollection<UserMission> UserMissions { get; set; } = new List<UserMission>();
    }

    public class UserMission
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string UserId { get; set; } = string.Empty;
        public string MissionId { get; set; } = string.Empty;
        public int Progress { get; set; } = 0;
        public bool IsCompleted { get; set; } = false;
        public DateTime? CompletedAt { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Navigation properties
        public User User { get; set; } = null!;
        public Mission Mission { get; set; } = null!;
    }

    public class Achievement
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Title { get; set; } = string.Empty;
        public string Icon { get; set; } = string.Empty;
        public string ImageUrl { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty; // "activity_count", "food_count", "level", etc.
        public int RequiredValue { get; set; }
        public int RewardExperience { get; set; }
        public bool IsActive { get; set; } = true;
        public DateTime? UnlockedAt { get; set; }

        // Navigation properties
        public ICollection<UserAchievement> UserAchievements { get; set; } = new List<UserAchievement>();
    }

    public class UserAchievement
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string UserId { get; set; } = string.Empty;
        public string AchievementId { get; set; } = string.Empty;
        public int CurrentProgress { get; set; } = 0;
        public DateTime? UnlockedAt { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Navigation properties
        public User User { get; set; } = null!;
        public Achievement Achievement { get; set; } = null!;
    }
}
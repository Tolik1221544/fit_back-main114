namespace FitnessTracker.API.Models
{
    public class Mission
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Title { get; set; } = string.Empty;
        public string Icon { get; set; } = string.Empty; // Ёмодзи
        public int RewardExperience { get; set; } // ќпыт вместо монет
        public string Type { get; set; } = string.Empty;
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
        public DateTime StartedAt { get; set; } = DateTime.UtcNow;

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
        public DateTime UnlockedAt { get; set; } = DateTime.UtcNow;
    }
}

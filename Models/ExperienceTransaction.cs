namespace FitnessTracker.API.Models
{
    public class ExperienceTransaction
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string UserId { get; set; } = string.Empty;
        public int Experience { get; set; }
        public string Source { get; set; } = string.Empty; // "mission", "daily_goal", "achievement"
        public string Description { get; set; } = string.Empty;
        public int LevelBefore { get; set; }
        public int LevelAfter { get; set; }
        public bool LeveledUp { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Navigation property
        public User User { get; set; } = null!;
    }
}
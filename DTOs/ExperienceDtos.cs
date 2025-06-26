namespace FitnessTracker.API.DTOs
{
    public class ExperienceTransactionDto
    {
        public string Id { get; set; } = string.Empty;
        public int Experience { get; set; }
        public string Source { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public int LevelBefore { get; set; }
        public int LevelAfter { get; set; }
        public bool LeveledUp { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
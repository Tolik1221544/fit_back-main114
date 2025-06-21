namespace FitnessTracker.API.Models
{
    public class Activity
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string UserId { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty; // "strength", "cardio"
        public DateTime StartDate { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // JSON данные для гибкости
        public string? StrengthDataJson { get; set; }
        public string? CardioDataJson { get; set; }

        // Navigation property
        public User User { get; set; } = null!;
    }

    public class StrengthData
    {
        public string Name { get; set; } = string.Empty;
        public string MuscleGroup { get; set; } = string.Empty;
        public string Equipment { get; set; } = string.Empty;
        public decimal WorkingWeight { get; set; }
        public int RestTimeSeconds { get; set; }
    }

    public class CardioData
    {
        public string CardioType { get; set; } = string.Empty;
        public decimal DistanceKm { get; set; }
        public int AvgPulse { get; set; }
        public int MaxPulse { get; set; }
        public string AvgPace { get; set; } = string.Empty;
    }
}
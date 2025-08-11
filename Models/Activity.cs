using System.Text.Json;

namespace FitnessTracker.API.Models
{
    public class Activity
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string UserId { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty; // "strength", "cardio"
        public DateTime StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public int? Calories { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public string? ActivityDataJson { get; set; }
        public User User { get; set; } = null!;

        public ActivityData? ActivityData
        {
            get => string.IsNullOrEmpty(ActivityDataJson) ? null : JsonSerializer.Deserialize<ActivityData>(ActivityDataJson);
            set => ActivityDataJson = value == null ? null : JsonSerializer.Serialize(value);
        }
    }

    public class ActivityData
    {
        public string Name { get; set; } = string.Empty;
        public string? Category { get; set; }
        public string? MuscleGroup { get; set; }
        public string? Equipment { get; set; }

        public decimal? Weight { get; set; }
        public int? RestTimeSeconds { get; set; }
        public List<ActivitySet>? Sets { get; set; }

        public decimal? Distance { get; set; }
        public string? AvgPace { get; set; }
        public int? AvgPulse { get; set; }
        public int? MaxPulse { get; set; }

        public int? Count { get; set; }
    }

    public class ActivitySet
    {
        public int SetNumber { get; set; }
        public decimal? Weight { get; set; }
        public int Reps { get; set; }
        public bool IsCompleted { get; set; } = true;
    }
}
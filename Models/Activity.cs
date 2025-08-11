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
        public DateTime StartTime { get; set; }
        public DateTime? EndTime { get; set; }
        public int? Calories { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public string? ActivityDataJson { get; set; }
        public string? StrengthDataJson { get; set; } 
        public string? CardioDataJson { get; set; } 

        public User User { get; set; } = null!;

        public ActivityData? ActivityData
        {
            get => string.IsNullOrEmpty(ActivityDataJson) ? null : JsonSerializer.Deserialize<ActivityData>(ActivityDataJson);
            set => ActivityDataJson = value == null ? null : JsonSerializer.Serialize(value);
        }

        public StrengthData? StrengthData
        {
            get => string.IsNullOrEmpty(StrengthDataJson) ? null : JsonSerializer.Deserialize<StrengthData>(StrengthDataJson);
            set => StrengthDataJson = value == null ? null : JsonSerializer.Serialize(value);
        }

        public CardioData? CardioData
        {
            get => string.IsNullOrEmpty(CardioDataJson) ? null : JsonSerializer.Deserialize<CardioData>(CardioDataJson);
            set => CardioDataJson = value == null ? null : JsonSerializer.Serialize(value);
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

    public class StrengthData
    {
        public string Name { get; set; } = string.Empty;
        public string MuscleGroup { get; set; } = string.Empty;
        public string Equipment { get; set; } = string.Empty;
        public decimal WorkingWeight { get; set; }
        public int RestTimeSeconds { get; set; }
        public List<StrengthSet> Sets { get; set; } = new List<StrengthSet>();
        public int TotalSets => Sets.Count;
        public int TotalReps => Sets.Sum(s => s.Reps);
        public PlankData? PlankData { get; set; }
    }

    public class StrengthSet
    {
        public int SetNumber { get; set; }
        public decimal Weight { get; set; }
        public int Reps { get; set; }
        public bool IsCompleted { get; set; } = true;
        public string? Notes { get; set; }
    }

    public class CardioData
    {
        public string CardioType { get; set; } = string.Empty;
        public decimal? DistanceKm { get; set; }
        public int? AvgPulse { get; set; }
        public int? MaxPulse { get; set; }
        public string AvgPace { get; set; } = string.Empty;
        public JumpRopeData? JumpRopeData { get; set; }
    }

    public class PlankData
    {
        public int DurationSeconds { get; set; }
        public string PlankType { get; set; } = string.Empty;
        public string Notes { get; set; } = string.Empty;
    }

    public class JumpRopeData
    {
        public int JumpCount { get; set; }
        public int DurationSeconds { get; set; }
        public string RopeType { get; set; } = string.Empty;
        public int? IntervalsCount { get; set; }
        public string Notes { get; set; } = string.Empty;
    }
}
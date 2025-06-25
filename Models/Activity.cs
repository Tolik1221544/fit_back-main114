using System.Text.Json;
using System.ComponentModel.DataAnnotations;

namespace FitnessTracker.API.Models
{
    public class Activity
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string UserId { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty; // "strength" or "cardio"
        public DateTime StartDate { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime? EndDate { get; set; } 
        public DateTime? EndTime { get; set; } 
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public int? Calories { get; set; } // Калории
        public string StrengthDataJson { get; set; } = string.Empty;
        public string CardioDataJson { get; set; } = string.Empty;

        // Navigation property
        public User User { get; set; } = null!;

        // Helper properties
        public StrengthData? StrengthData
        {
            get => string.IsNullOrEmpty(StrengthDataJson) ? null : JsonSerializer.Deserialize<StrengthData>(StrengthDataJson);
            set => StrengthDataJson = value == null ? string.Empty : JsonSerializer.Serialize(value);
        }

        public CardioData? CardioData
        {
            get => string.IsNullOrEmpty(CardioDataJson) ? null : JsonSerializer.Deserialize<CardioData>(CardioDataJson);
            set => CardioDataJson = value == null ? string.Empty : JsonSerializer.Serialize(value);
        }
    }

    public class Steps
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string UserId { get; set; } = string.Empty;
        public int StepsCount { get; set; }
        public int? Calories { get; set; }
        public DateTime Date { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Navigation property
        public User User { get; set; } = null!;
    }

    public class StrengthData
    {
        public string? Name { get; set; }
        public string? MuscleGroup { get; set; }
        public string? Equipment { get; set; }
        public decimal? WorkingWeight { get; set; }
        public int? RestTimeSeconds { get; set; }
    }

    public class CardioData
    {
        public string? CardioType { get; set; }
        public decimal? DistanceKm { get; set; }
        public int? AvgPulse { get; set; }
        public int? MaxPulse { get; set; }
        public string? AvgPace { get; set; }
    }
}
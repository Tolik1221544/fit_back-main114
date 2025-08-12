namespace FitnessTracker.API.DTOs
{
    public class ActivityDto
    {
        public string Id { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty; // "strength", "cardio"
        public DateTime StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public int? Calories { get; set; }
        public DateTime CreatedAt { get; set; }
        public ActivityDataDto? ActivityData { get; set; }

        public DateTime StartTime { get; set; }
        public DateTime? EndTime { get; set; }
        public int? EstimatedCalories { get; set; }
        public StrengthDataDto? StrengthData { get; set; }
        public CardioDataDto? CardioData { get; set; }
    }

    public class AddActivityRequest
    {
        public string Type { get; set; } = string.Empty;
        public DateTime StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public int? Calories { get; set; }
        public ActivityDataDto? ActivityData { get; set; }

        public DateTime StartTime { get; set; }
        public DateTime? EndTime { get; set; }
        public StrengthDataDto? StrengthData { get; set; }
        public CardioDataDto? CardioData { get; set; }
    }

    public class UpdateActivityRequest
    {
        public string Type { get; set; } = string.Empty;
        public DateTime StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public int? Calories { get; set; }
        public ActivityDataDto? ActivityData { get; set; }
    }

    public class ActivityDataDto
    {
        public string Name { get; set; } = string.Empty;
        public string? Category { get; set; }
        public string? Equipment { get; set; }
        public int? Count { get; set; }

        // Strength fields
        public string? MuscleGroup { get; set; }
        public decimal? Weight { get; set; }
        public int? RestTimeSeconds { get; set; }
        public List<ActivitySetDto>? Sets { get; set; }

        // Cardio fields
        public decimal? Distance { get; set; }
        public string? AvgPace { get; set; }
        public int? AvgPulse { get; set; }
        public int? MaxPulse { get; set; }
    }

    public class ActivitySetDto
    {
        public int SetNumber { get; set; }
        public decimal? Weight { get; set; }
        public int Reps { get; set; }
        public bool IsCompleted { get; set; } = true;
    }

    public class StrengthDataDto
    {
        public string Name { get; set; } = string.Empty;
        public string? MuscleGroup { get; set; }
        public string? Equipment { get; set; }
        public decimal WorkingWeight { get; set; }
        public int RestTimeSeconds { get; set; } = 120;
        public List<StrengthSetDto>? Sets { get; set; }
        public int TotalReps { get; set; }
    }

    public class StrengthSetDto
    {
        public int SetNumber { get; set; }
        public decimal Weight { get; set; }
        public int Reps { get; set; }
        public bool IsCompleted { get; set; } = true;
        public string? Notes { get; set; }
    }

    public class CardioDataDto
    {
        public string CardioType { get; set; } = string.Empty;
        public decimal? DistanceKm { get; set; }
        public string? AvgPace { get; set; }
        public int? AvgPulse { get; set; }
        public int? MaxPulse { get; set; }
        public JumpRopeDataDto? JumpRopeData { get; set; }
    }

    public class JumpRopeDataDto
    {
        public int JumpCount { get; set; }
    }

    public class StepsDto
    {
        public string Id { get; set; } = string.Empty;
        public string UserId { get; set; } = string.Empty;
        public int StepsCount { get; set; }
        public int? Calories { get; set; }
        public DateTime Date { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class AddStepsRequest
    {
        public int Steps { get; set; }
        public int? Calories { get; set; }
        public DateTime Date { get; set; } = DateTime.UtcNow.Date;
    }
}
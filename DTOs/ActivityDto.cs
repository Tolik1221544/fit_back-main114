namespace FitnessTracker.API.DTOs
{
    public class ActivityDto
    {
        public string Id { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty; // "strength", "cardio"
        public DateTime StartDate { get; set; }
        public DateTime? EndDate { get; set; }

        public DateTime? StartTime { get; set; }
        public DateTime? EndTime { get; set; }

        public int? Calories { get; set; }
        public DateTime CreatedAt { get; set; }
        public StrengthDataDto? StrengthData { get; set; }
        public CardioDataDto? CardioData { get; set; }
    }

    public class AddActivityRequest
    {
        public string Type { get; set; } = string.Empty; // "strength", "cardio"
        public DateTime StartDate { get; set; }
        public DateTime? EndDate { get; set; }

        public DateTime? StartTime { get; set; }
        public DateTime? EndTime { get; set; }

        public int? Calories { get; set; }
        public StrengthDataDto? StrengthData { get; set; }
        public CardioDataDto? CardioData { get; set; }
    }

    public class UpdateActivityRequest
    {
        public string Type { get; set; } = string.Empty;
        public DateTime StartDate { get; set; }
        public DateTime? EndDate { get; set; }

        public DateTime? StartTime { get; set; }
        public DateTime? EndTime { get; set; }

        public int? Calories { get; set; }
        public StrengthDataDto? StrengthData { get; set; }
        public CardioDataDto? CardioData { get; set; }
    }

    public class StrengthDataDto
    {
        public string Name { get; set; } = string.Empty;
        public string MuscleGroup { get; set; } = string.Empty;
        public string Equipment { get; set; } = string.Empty;
        public decimal WorkingWeight { get; set; }
        public int RestTimeSeconds { get; set; }

        public List<StrengthSetDto> Sets { get; set; } = new List<StrengthSetDto>();
        public int TotalSets => Sets.Count;
        public int TotalReps => Sets.Sum(s => s.Reps);
        public PlankDataDto? PlankData { get; set; }
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
        public int? AvgPulse { get; set; }
        public int? MaxPulse { get; set; }
        public string AvgPace { get; set; } = string.Empty;
        public JumpRopeDataDto? JumpRopeData { get; set; }
    }

    public class PlankDataDto
    {
        public int DurationSeconds { get; set; }
        public string PlankType { get; set; } = string.Empty; 
        public string Notes { get; set; } = string.Empty;
    }

    public class JumpRopeDataDto
    {
        public int JumpCount { get; set; }
        public int DurationSeconds { get; set; }
        public string RopeType { get; set; } = string.Empty; 
        public int? IntervalsCount { get; set; }
        public string Notes { get; set; } = string.Empty;
    }

    public class StepsDto
    {
        public string Id { get; set; } = string.Empty;
        public int StepsCount { get; set; }
        public int? Calories { get; set; }
        public DateTime Date { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class AddStepsRequest
    {
        public int Steps { get; set; }
        public int? Calories { get; set; }
        public DateTime Date { get; set; } = DateTime.UtcNow;
    }

    public class VoiceWorkoutData
    {
        public string Type { get; set; } = string.Empty;
        public DateTime StartTime { get; set; } = DateTime.UtcNow;
        public DateTime? EndTime { get; set; } = DateTime.UtcNow.AddMinutes(30);
        public int? EstimatedCalories { get; set; }
        public StrengthDataDto? StrengthData { get; set; }
        public CardioDataDto? CardioData { get; set; }
        public List<string> Notes { get; set; } = new List<string>();
    }
}
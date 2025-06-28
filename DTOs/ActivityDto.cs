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

        // ✅ ДОБАВЛЕНО: Поддержка старых полей (для совместимости)
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
    }

    public class CardioDataDto
    {
        public string CardioType { get; set; } = string.Empty;
        public decimal? DistanceKm { get; set; }
        public int? AvgPulse { get; set; }
        public int? MaxPulse { get; set; }
        public string AvgPace { get; set; } = string.Empty;
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
}
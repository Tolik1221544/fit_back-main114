namespace FitnessTracker.API.Models
{
    /// <summary>
    /// 🎯 Цели пользователя (похудение, сохранение веса, набор массы)
    /// </summary>
    public class Goal
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string UserId { get; set; } = string.Empty;

        // Основная цель
        public string GoalType { get; set; } = string.Empty; // "weight_loss", "weight_maintain", "muscle_gain"
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;

        // Целевые показатели
        public decimal? TargetWeight { get; set; }
        public decimal? CurrentWeight { get; set; }
        public int? TargetCalories { get; set; }
        public int? TargetProtein { get; set; }
        public int? TargetCarbs { get; set; }
        public int? TargetFats { get; set; }

        // Активность
        public int? TargetWorkoutsPerWeek { get; set; }
        public int? TargetStepsPerDay { get; set; }
        public int? TargetActiveMinutes { get; set; }

        // Статус
        public bool IsActive { get; set; } = true;
        public DateTime StartDate { get; set; } = DateTime.UtcNow;
        public DateTime? EndDate { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? CompletedAt { get; set; }

        // Прогресс
        public decimal ProgressPercentage { get; set; } = 0;
        public string Status { get; set; } = "active"; // "active", "completed", "paused"

        // Navigation property
        public User User { get; set; } = null!;
        public ICollection<DailyGoalProgress> DailyProgress { get; set; } = new List<DailyGoalProgress>();
    }

    /// <summary>
    /// 📊 Ежедневный прогресс по целям
    /// </summary>
    public class DailyGoalProgress
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string GoalId { get; set; } = string.Empty;
        public string UserId { get; set; } = string.Empty;
        public DateTime Date { get; set; }

        // Фактические показатели за день
        public int ActualCalories { get; set; } = 0;
        public decimal ActualProtein { get; set; } = 0;
        public decimal ActualCarbs { get; set; } = 0;
        public decimal ActualFats { get; set; } = 0;
        public int ActualSteps { get; set; } = 0;
        public int ActualWorkouts { get; set; } = 0;
        public int ActualActiveMinutes { get; set; } = 0;
        public decimal? ActualWeight { get; set; }

        // Прогресс в процентах по каждой метрике
        public decimal CaloriesProgress { get; set; } = 0;
        public decimal ProteinProgress { get; set; } = 0;
        public decimal CarbsProgress { get; set; } = 0;
        public decimal FatsProgress { get; set; } = 0;
        public decimal StepsProgress { get; set; } = 0;
        public decimal WorkoutProgress { get; set; } = 0;

        // Общий прогресс за день
        public decimal OverallProgress { get; set; } = 0;
        public bool IsCompleted { get; set; } = false;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        // Navigation properties
        public Goal Goal { get; set; } = null!;
        public User User { get; set; } = null!;
    }
}
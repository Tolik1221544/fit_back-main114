namespace FitnessTracker.API.DTOs
{
    /// <summary>
    /// 🎯 DTO для отображения цели
    /// </summary>
    public class GoalDto
    {
        public string Id { get; set; } = string.Empty;
        public string GoalType { get; set; } = string.Empty;
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
        public bool IsActive { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public decimal ProgressPercentage { get; set; }
        public string Status { get; set; } = string.Empty;

        // Прогресс за сегодня
        public DailyGoalProgressDto? TodayProgress { get; set; }

        // Статистика
        public int TotalDays { get; set; }
        public int CompletedDays { get; set; }
        public decimal AverageProgress { get; set; }
    }

    /// <summary>
    /// ➕ Запрос на создание цели
    /// </summary>
    public class CreateGoalRequest
    {
        public string GoalType { get; set; } = string.Empty; // "weight_loss", "weight_maintain", "muscle_gain"
        public string? Title { get; set; }
        public string? Description { get; set; }

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

        public DateTime? EndDate { get; set; }
    }

    /// <summary>
    /// ✏️ Запрос на обновление цели
    /// </summary>
    public class UpdateGoalRequest
    {
        public string? Title { get; set; }
        public string? Description { get; set; }

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

        public DateTime? EndDate { get; set; }
        public string? Status { get; set; }
    }

    /// <summary>
    /// 📊 DTO для ежедневного прогресса
    /// </summary>
    public class DailyGoalProgressDto
    {
        public string Id { get; set; } = string.Empty;
        public DateTime Date { get; set; }

        // Фактические показатели
        public int ActualCalories { get; set; }
        public decimal ActualProtein { get; set; }
        public decimal ActualCarbs { get; set; }
        public decimal ActualFats { get; set; }
        public int ActualSteps { get; set; }
        public int ActualWorkouts { get; set; }
        public int ActualActiveMinutes { get; set; }
        public decimal? ActualWeight { get; set; }

        // Прогресс в процентах
        public decimal CaloriesProgress { get; set; }
        public decimal ProteinProgress { get; set; }
        public decimal CarbsProgress { get; set; }
        public decimal FatsProgress { get; set; }
        public decimal StepsProgress { get; set; }
        public decimal WorkoutProgress { get; set; }

        // Общий прогресс
        public decimal OverallProgress { get; set; }
        public bool IsCompleted { get; set; }

        // Целевые показатели для сравнения
        public int? TargetCalories { get; set; }
        public int? TargetProtein { get; set; }
        public int? TargetCarbs { get; set; }
        public int? TargetFats { get; set; }
        public int? TargetSteps { get; set; }
        public int? TargetWorkouts { get; set; }
    }

    /// <summary>
    /// 📈 Обновление прогресса за день
    /// </summary>
    public class UpdateDailyProgressRequest
    {
        public DateTime Date { get; set; } = DateTime.UtcNow.Date;
        public decimal? ActualWeight { get; set; }

        // Опционально - система может автоматически рассчитать из активностей и питания
        public int? ManualCalories { get; set; }
        public decimal? ManualProtein { get; set; }
        public decimal? ManualCarbs { get; set; }
        public decimal? ManualFats { get; set; }
        public int? ManualSteps { get; set; }
        public int? ManualWorkouts { get; set; }
        public int? ManualActiveMinutes { get; set; }
    }

    /// <summary>
    /// 📋 Предустановленные шаблоны целей
    /// </summary>
    public class GoalTemplateDto
    {
        public string GoalType { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Icon { get; set; } = string.Empty;

        // Рекомендуемые значения
        public int? RecommendedCalories { get; set; }
        public int? RecommendedProtein { get; set; }
        public int? RecommendedCarbs { get; set; }
        public int? RecommendedFats { get; set; }
        public int? RecommendedWorkoutsPerWeek { get; set; }
        public int? RecommendedStepsPerDay { get; set; }
        public int? RecommendedActiveMinutes { get; set; }

        public List<string> Tips { get; set; } = new List<string>();
    }
}
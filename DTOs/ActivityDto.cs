using System.ComponentModel.DataAnnotations;

namespace FitnessTracker.API.DTOs
{
    /// <summary>
    /// Запрос для добавления новой активности
    /// </summary>
    public class AddActivityRequest
    {
        /// <summary>
        /// Тип тренировки: "strength" для силовой или "cardio" для кардио
        /// </summary>
        /// <example>strength</example>
        [Required]
        public string Type { get; set; } = string.Empty;

        /// <summary>
        /// Дата начала тренировки
        /// </summary>
        /// <example>2025-06-26T10:00:00Z</example>
        [Required]
        public DateTime StartDate { get; set; }

        /// <summary>
        /// Время начала тренировки
        /// </summary>
        /// <example>2025-06-26T10:00:00Z</example>
        [Required]
        public DateTime StartTime { get; set; }

        /// <summary>
        /// Дата окончания тренировки (опционально)
        /// </summary>
        /// <example>2025-06-26T11:00:00Z</example>
        public DateTime? EndDate { get; set; }

        /// <summary>
        /// Время окончания тренировки (опционально)
        /// </summary>
        /// <example>2025-06-26T11:00:00Z</example>
        public DateTime? EndTime { get; set; }

        /// <summary>
        /// Количество сожженных калорий
        /// </summary>
        /// <example>300</example>
        public int? Calories { get; set; }

        /// <summary>
        /// Данные силовой тренировки (заполнять ТОЛЬКО для type="strength")
        /// </summary>
        public StrengthDataDto? StrengthData { get; set; }

        /// <summary>
        /// Данные кардио тренировки (заполнять ТОЛЬКО для type="cardio")
        /// </summary>
        public CardioDataDto? CardioData { get; set; }
    }

    /// <summary>
    /// Запрос для обновления активности
    /// </summary>
    public class UpdateActivityRequest
    {
        /// <summary>
        /// Тип тренировки: "strength" или "cardio"
        /// </summary>
        /// <example>strength</example>
        [Required]
        public string Type { get; set; } = string.Empty;

        /// <summary>
        /// Дата начала тренировки
        /// </summary>
        /// <example>2025-06-26T10:00:00Z</example>
        [Required]
        public DateTime StartDate { get; set; }

        /// <summary>
        /// Время начала тренировки
        /// </summary>
        /// <example>2025-06-26T10:00:00Z</example>
        [Required]
        public DateTime StartTime { get; set; }

        /// <summary>
        /// Дата окончания тренировки
        /// </summary>
        /// <example>2025-06-26T11:00:00Z</example>
        public DateTime? EndDate { get; set; }

        /// <summary>
        /// Время окончания тренировки
        /// </summary>
        /// <example>2025-06-26T11:00:00Z</example>
        public DateTime? EndTime { get; set; }

        /// <summary>
        /// Количество сожженных калорий
        /// </summary>
        /// <example>300</example>
        public int? Calories { get; set; }

        /// <summary>
        /// Данные силовой тренировки
        /// </summary>
        public StrengthDataDto? StrengthData { get; set; }

        /// <summary>
        /// Данные кардио тренировки
        /// </summary>
        public CardioDataDto? CardioData { get; set; }
    }

    /// <summary>
    /// Информация об активности
    /// </summary>
    public class ActivityDto
    {
        /// <summary>
        /// Уникальный ID активности
        /// </summary>
        /// <example>550e8400-e29b-41d4-a716-446655440000</example>
        public string Id { get; set; } = string.Empty;

        /// <summary>
        /// Тип тренировки
        /// </summary>
        /// <example>strength</example>
        public string Type { get; set; } = string.Empty;

        /// <summary>
        /// Дата начала
        /// </summary>
        /// <example>2025-06-26T10:00:00Z</example>
        public DateTime StartDate { get; set; }

        /// <summary>
        /// Время начала
        /// </summary>
        /// <example>2025-06-26T10:00:00Z</example>
        public DateTime StartTime { get; set; }

        /// <summary>
        /// Дата окончания
        /// </summary>
        /// <example>2025-06-26T11:00:00Z</example>
        public DateTime? EndDate { get; set; }

        /// <summary>
        /// Время окончания
        /// </summary>
        /// <example>2025-06-26T11:00:00Z</example>
        public DateTime? EndTime { get; set; }

        /// <summary>
        /// Калории
        /// </summary>
        /// <example>300</example>
        public int? Calories { get; set; }

        /// <summary>
        /// Дата создания записи
        /// </summary>
        /// <example>2025-06-26T10:00:00Z</example>
        public DateTime CreatedAt { get; set; }

        /// <summary>
        /// Данные силовой тренировки (только для strength)
        /// </summary>
        public StrengthDataDto? StrengthData { get; set; }

        /// <summary>
        /// Данные кардио тренировки (только для cardio)
        /// </summary>
        public CardioDataDto? CardioData { get; set; }
    }

    /// <summary>
    /// Данные силовой тренировки
    /// </summary>
    public class StrengthDataDto
    {
        /// <summary>
        /// Название упражнения
        /// </summary>
        /// <example>Жим лежа</example>
        [Required]
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Группа мышц
        /// </summary>
        /// <example>Грудь</example>
        [Required]
        public string MuscleGroup { get; set; } = string.Empty;

        /// <summary>
        /// Используемое оборудование
        /// </summary>
        /// <example>Штанга</example>
        [Required]
        public string Equipment { get; set; } = string.Empty;

        /// <summary>
        /// Рабочий вес в кг
        /// </summary>
        /// <example>80</example>
        [Required]
        public decimal WorkingWeight { get; set; }

        /// <summary>
        /// Время отдыха между подходами в секундах
        /// </summary>
        /// <example>120</example>
        [Required]
        public int RestTimeSeconds { get; set; }
    }

    /// <summary>
    /// Данные кардио тренировки
    /// </summary>
    public class CardioDataDto
    {
        /// <summary>
        /// Тип кардио тренировки
        /// </summary>
        /// <example>Бег</example>
        [Required]
        public string CardioType { get; set; } = string.Empty;

        /// <summary>
        /// Дистанция в километрах (опционально)
        /// </summary>
        /// <example>5.0</example>
        public decimal? DistanceKm { get; set; }

        /// <summary>
        /// Средний пульс (опционально)
        /// </summary>
        /// <example>150</example>
        public int? AvgPulse { get; set; }

        /// <summary>
        /// Максимальный пульс (опционально)
        /// </summary>
        /// <example>170</example>
        public int? MaxPulse { get; set; }

        /// <summary>
        /// Средний темп (опционально)
        /// </summary>
        /// <example>5:30</example>
        public string AvgPace { get; set; } = string.Empty;
    }

    /// <summary>
    /// Запрос для добавления шагов
    /// </summary>
    public class AddStepsRequest
    {
        /// <summary>
        /// Количество шагов
        /// </summary>
        /// <example>10000</example>
        [Required]
        [Range(1, int.MaxValue, ErrorMessage = "Количество шагов должно быть больше 0")]
        public int Steps { get; set; }

        /// <summary>
        /// Сожженные калории (опционально)
        /// </summary>
        /// <example>500</example>
        public int? Calories { get; set; }

        /// <summary>
        /// Дата для которой записываются шаги
        /// </summary>
        /// <example>2025-06-26T00:00:00Z</example>
        [Required]
        public DateTime Date { get; set; } = DateTime.Today;
    }

    /// <summary>
    /// Информация о шагах
    /// </summary>
    public class StepsDto
    {
        /// <summary>
        /// ID записи
        /// </summary>
        /// <example>550e8400-e29b-41d4-a716-446655440000</example>
        public string Id { get; set; } = string.Empty;

        /// <summary>
        /// Количество шагов
        /// </summary>
        /// <example>10000</example>
        public int StepsCount { get; set; }

        /// <summary>
        /// Сожженные калории
        /// </summary>
        /// <example>500</example>
        public int? Calories { get; set; }

        /// <summary>
        /// Дата
        /// </summary>
        /// <example>2025-06-26T00:00:00Z</example>
        public DateTime Date { get; set; }

        /// <summary>
        /// Дата создания записи
        /// </summary>
        /// <example>2025-06-26T10:00:00Z</example>
        public DateTime CreatedAt { get; set; }
    }
}
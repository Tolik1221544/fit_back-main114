using System.ComponentModel.DataAnnotations;

namespace FitnessTracker.API.DTOs
{
    /// <summary>
    /// ������ ��� ���������� ����� ����������
    /// </summary>
    public class AddActivityRequest
    {
        /// <summary>
        /// ��� ����������: "strength" ��� ������� ��� "cardio" ��� ������
        /// </summary>
        /// <example>strength</example>
        [Required]
        public string Type { get; set; } = string.Empty;

        /// <summary>
        /// ���� ������ ����������
        /// </summary>
        /// <example>2025-06-26T10:00:00Z</example>
        [Required]
        public DateTime StartDate { get; set; }

        /// <summary>
        /// ����� ������ ����������
        /// </summary>
        /// <example>2025-06-26T10:00:00Z</example>
        [Required]
        public DateTime StartTime { get; set; }

        /// <summary>
        /// ���� ��������� ���������� (�����������)
        /// </summary>
        /// <example>2025-06-26T11:00:00Z</example>
        public DateTime? EndDate { get; set; }

        /// <summary>
        /// ����� ��������� ���������� (�����������)
        /// </summary>
        /// <example>2025-06-26T11:00:00Z</example>
        public DateTime? EndTime { get; set; }

        /// <summary>
        /// ���������� ��������� �������
        /// </summary>
        /// <example>300</example>
        public int? Calories { get; set; }

        /// <summary>
        /// ������ ������� ���������� (��������� ������ ��� type="strength")
        /// </summary>
        public StrengthDataDto? StrengthData { get; set; }

        /// <summary>
        /// ������ ������ ���������� (��������� ������ ��� type="cardio")
        /// </summary>
        public CardioDataDto? CardioData { get; set; }
    }

    /// <summary>
    /// ������ ��� ���������� ����������
    /// </summary>
    public class UpdateActivityRequest
    {
        /// <summary>
        /// ��� ����������: "strength" ��� "cardio"
        /// </summary>
        /// <example>strength</example>
        [Required]
        public string Type { get; set; } = string.Empty;

        /// <summary>
        /// ���� ������ ����������
        /// </summary>
        /// <example>2025-06-26T10:00:00Z</example>
        [Required]
        public DateTime StartDate { get; set; }

        /// <summary>
        /// ����� ������ ����������
        /// </summary>
        /// <example>2025-06-26T10:00:00Z</example>
        [Required]
        public DateTime StartTime { get; set; }

        /// <summary>
        /// ���� ��������� ����������
        /// </summary>
        /// <example>2025-06-26T11:00:00Z</example>
        public DateTime? EndDate { get; set; }

        /// <summary>
        /// ����� ��������� ����������
        /// </summary>
        /// <example>2025-06-26T11:00:00Z</example>
        public DateTime? EndTime { get; set; }

        /// <summary>
        /// ���������� ��������� �������
        /// </summary>
        /// <example>300</example>
        public int? Calories { get; set; }

        /// <summary>
        /// ������ ������� ����������
        /// </summary>
        public StrengthDataDto? StrengthData { get; set; }

        /// <summary>
        /// ������ ������ ����������
        /// </summary>
        public CardioDataDto? CardioData { get; set; }
    }

    /// <summary>
    /// ���������� �� ����������
    /// </summary>
    public class ActivityDto
    {
        /// <summary>
        /// ���������� ID ����������
        /// </summary>
        /// <example>550e8400-e29b-41d4-a716-446655440000</example>
        public string Id { get; set; } = string.Empty;

        /// <summary>
        /// ��� ����������
        /// </summary>
        /// <example>strength</example>
        public string Type { get; set; } = string.Empty;

        /// <summary>
        /// ���� ������
        /// </summary>
        /// <example>2025-06-26T10:00:00Z</example>
        public DateTime StartDate { get; set; }

        /// <summary>
        /// ����� ������
        /// </summary>
        /// <example>2025-06-26T10:00:00Z</example>
        public DateTime StartTime { get; set; }

        /// <summary>
        /// ���� ���������
        /// </summary>
        /// <example>2025-06-26T11:00:00Z</example>
        public DateTime? EndDate { get; set; }

        /// <summary>
        /// ����� ���������
        /// </summary>
        /// <example>2025-06-26T11:00:00Z</example>
        public DateTime? EndTime { get; set; }

        /// <summary>
        /// �������
        /// </summary>
        /// <example>300</example>
        public int? Calories { get; set; }

        /// <summary>
        /// ���� �������� ������
        /// </summary>
        /// <example>2025-06-26T10:00:00Z</example>
        public DateTime CreatedAt { get; set; }

        /// <summary>
        /// ������ ������� ���������� (������ ��� strength)
        /// </summary>
        public StrengthDataDto? StrengthData { get; set; }

        /// <summary>
        /// ������ ������ ���������� (������ ��� cardio)
        /// </summary>
        public CardioDataDto? CardioData { get; set; }
    }

    /// <summary>
    /// ������ ������� ����������
    /// </summary>
    public class StrengthDataDto
    {
        /// <summary>
        /// �������� ����������
        /// </summary>
        /// <example>��� ����</example>
        [Required]
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// ������ ����
        /// </summary>
        /// <example>�����</example>
        [Required]
        public string MuscleGroup { get; set; } = string.Empty;

        /// <summary>
        /// ������������ ������������
        /// </summary>
        /// <example>������</example>
        [Required]
        public string Equipment { get; set; } = string.Empty;

        /// <summary>
        /// ������� ��� � ��
        /// </summary>
        /// <example>80</example>
        [Required]
        public decimal WorkingWeight { get; set; }

        /// <summary>
        /// ����� ������ ����� ��������� � ��������
        /// </summary>
        /// <example>120</example>
        [Required]
        public int RestTimeSeconds { get; set; }
    }

    /// <summary>
    /// ������ ������ ����������
    /// </summary>
    public class CardioDataDto
    {
        /// <summary>
        /// ��� ������ ����������
        /// </summary>
        /// <example>���</example>
        [Required]
        public string CardioType { get; set; } = string.Empty;

        /// <summary>
        /// ��������� � ���������� (�����������)
        /// </summary>
        /// <example>5.0</example>
        public decimal? DistanceKm { get; set; }

        /// <summary>
        /// ������� ����� (�����������)
        /// </summary>
        /// <example>150</example>
        public int? AvgPulse { get; set; }

        /// <summary>
        /// ������������ ����� (�����������)
        /// </summary>
        /// <example>170</example>
        public int? MaxPulse { get; set; }

        /// <summary>
        /// ������� ���� (�����������)
        /// </summary>
        /// <example>5:30</example>
        public string AvgPace { get; set; } = string.Empty;
    }

    /// <summary>
    /// ������ ��� ���������� �����
    /// </summary>
    public class AddStepsRequest
    {
        /// <summary>
        /// ���������� �����
        /// </summary>
        /// <example>10000</example>
        [Required]
        [Range(1, int.MaxValue, ErrorMessage = "���������� ����� ������ ���� ������ 0")]
        public int Steps { get; set; }

        /// <summary>
        /// ��������� ������� (�����������)
        /// </summary>
        /// <example>500</example>
        public int? Calories { get; set; }

        /// <summary>
        /// ���� ��� ������� ������������ ����
        /// </summary>
        /// <example>2025-06-26T00:00:00Z</example>
        [Required]
        public DateTime Date { get; set; } = DateTime.Today;
    }

    /// <summary>
    /// ���������� � �����
    /// </summary>
    public class StepsDto
    {
        /// <summary>
        /// ID ������
        /// </summary>
        /// <example>550e8400-e29b-41d4-a716-446655440000</example>
        public string Id { get; set; } = string.Empty;

        /// <summary>
        /// ���������� �����
        /// </summary>
        /// <example>10000</example>
        public int StepsCount { get; set; }

        /// <summary>
        /// ��������� �������
        /// </summary>
        /// <example>500</example>
        public int? Calories { get; set; }

        /// <summary>
        /// ����
        /// </summary>
        /// <example>2025-06-26T00:00:00Z</example>
        public DateTime Date { get; set; }

        /// <summary>
        /// ���� �������� ������
        /// </summary>
        /// <example>2025-06-26T10:00:00Z</example>
        public DateTime CreatedAt { get; set; }
    }
}
using System.Text.Json;

namespace FitnessTracker.API.Models
{
    public class Activity
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string UserId { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty; // "strength", "cardio"
        public DateTime StartDate { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime? EndDate { get; set; }
        public DateTime? EndTime { get; set; }

        public int? Calories { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Navigation property
        public User User { get; set; } = null!;

    
        public string? StrengthDataJson { get; set; }
        public string? CardioDataJson { get; set; }

      
        public StrengthData? StrengthData
        {
            get
            {
                if (string.IsNullOrEmpty(StrengthDataJson))
                    return null;

                try
                {
                    return JsonSerializer.Deserialize<StrengthData>(StrengthDataJson, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });
                }
                catch (JsonException ex)
                {
                    // Логируем ошибку десериализации
                    Console.WriteLine($"Error deserializing StrengthData: {ex.Message}");
                    return null;
                }
            }
            set
            {
                if (value == null)
                {
                    StrengthDataJson = null;
                }
                else
                {
                    try
                    {
                        StrengthDataJson = JsonSerializer.Serialize(value, new JsonSerializerOptions
                        {
                            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                        });
                    }
                    catch (JsonException ex)
                    {
                        Console.WriteLine($"Error serializing StrengthData: {ex.Message}");
                        StrengthDataJson = null;
                    }
                }
            }
        }

        public CardioData? CardioData
        {
            get
            {
                if (string.IsNullOrEmpty(CardioDataJson))
                    return null;

                try
                {
                    return JsonSerializer.Deserialize<CardioData>(CardioDataJson, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });
                }
                catch (JsonException ex)
                {
                    Console.WriteLine($"Error deserializing CardioData: {ex.Message}");
                    return null;
                }
            }
            set
            {
                if (value == null)
                {
                    CardioDataJson = null;
                }
                else
                {
                    try
                    {
                        CardioDataJson = JsonSerializer.Serialize(value, new JsonSerializerOptions
                        {
                            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                        });
                    }
                    catch (JsonException ex)
                    {
                        Console.WriteLine($"Error serializing CardioData: {ex.Message}");
                        CardioDataJson = null;
                    }
                }
            }
        }
    }


    public class StrengthData
    {
        public string Name { get; set; } = string.Empty;
        public string MuscleGroup { get; set; } = string.Empty;
        public string Equipment { get; set; } = string.Empty;
        public decimal WorkingWeight { get; set; }
        public int RestTimeSeconds { get; set; }

 
        public bool IsValid()
        {
            return !string.IsNullOrWhiteSpace(Name) &&
                   WorkingWeight >= 0 &&
                   RestTimeSeconds >= 0;
        }
    }

    public class CardioData
    {
        public string CardioType { get; set; } = string.Empty;
        public decimal? DistanceKm { get; set; }
        public int? AvgPulse { get; set; }
        public int? MaxPulse { get; set; }
        public string AvgPace { get; set; } = string.Empty;

        public bool IsValid()
        {
            return !string.IsNullOrWhiteSpace(CardioType) &&
                   (!DistanceKm.HasValue || DistanceKm >= 0) &&
                   (!AvgPulse.HasValue || AvgPulse >= 0) &&
                   (!MaxPulse.HasValue || MaxPulse >= 0);
        }
    }
}
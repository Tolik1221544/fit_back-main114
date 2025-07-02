namespace FitnessTracker.API.Models
{
    public class AiAnalysis
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string UserId { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty; // "food_photo", "voice_input", "chat"
        public string? ImageUrl { get; set; }
        public string InputText { get; set; } = string.Empty;
        public string Response { get; set; } = string.Empty;
        public int TokensUsed { get; set; }
        public decimal ProcessingTimeMs { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Navigation property
        public User User { get; set; } = null!;
    }
}
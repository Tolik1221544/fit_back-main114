namespace FitnessTracker.API.Services.AI.Models
{
    public class UniversalAIRequest
    {
        public List<UniversalMessage> Messages { get; set; } = new();
        public AIRequestConfig Config { get; set; } = new();
    }

    public class UniversalMessage
    {
        public string Role { get; set; } = "user"; // user, assistant, system
        public List<UniversalContent> Content { get; set; } = new();
    }

    public class UniversalContent
    {
        public string Type { get; set; } = "text"; // text, image, audio
        public string? Text { get; set; }
        public UniversalMedia? Media { get; set; }
    }

    public class UniversalMedia
    {
        public string MimeType { get; set; } = string.Empty;
        public byte[] Data { get; set; } = Array.Empty<byte>();
        public string? Base64Data { get; set; }
    }

    public class AIRequestConfig
    {
        public double Temperature { get; set; } = 0.1;
        public int MaxTokens { get; set; } = 2048;
        public double TopP { get; set; } = 1.0;
        public string? ResponseFormat { get; set; }
    }
}
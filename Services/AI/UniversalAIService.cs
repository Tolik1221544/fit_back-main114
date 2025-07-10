using FitnessTracker.API.DTOs;

namespace FitnessTracker.API.Services.AI
{
    public class UniversalAIService : IGeminiService
    {
        private readonly Dictionary<string, IAIProvider> _providers;
        private readonly IConfiguration _configuration;
        private readonly ILogger<UniversalAIService> _logger;

        public UniversalAIService(
            IEnumerable<IAIProvider> providers,
            IConfiguration configuration,
            ILogger<UniversalAIService> logger)
        {
            _providers = providers.ToDictionary(p => p.ProviderName, p => p);
            _configuration = configuration;
            _logger = logger;
        }

        public async Task<FoodScanResponse> AnalyzeFoodImageAsync(byte[] imageData, string? userPrompt = null)
        {
            var provider = GetActiveProvider();
            _logger.LogInformation($"?? Using {provider.ProviderName} for food analysis");
            return await provider.AnalyzeFoodImageAsync(imageData, userPrompt);
        }

        public async Task<BodyScanResponse> AnalyzeBodyImagesAsync(
            byte[]? frontImageData,
            byte[]? sideImageData,
            byte[]? backImageData,
            decimal? weight = null,
            decimal? height = null,
            int? age = null,
            string? gender = null,
            string? goals = null)
        {
            var provider = GetActiveProvider();
            _logger.LogInformation($"?? Using {provider.ProviderName} for body analysis");
            return await provider.AnalyzeBodyImagesAsync(frontImageData, sideImageData, backImageData, weight, height, age, gender, goals);
        }

        public async Task<VoiceWorkoutResponse> AnalyzeVoiceWorkoutAsync(byte[] audioData, string? workoutType = null)
        {
            var provider = GetActiveProvider();
            _logger.LogInformation($"?? Using {provider.ProviderName} for voice workout analysis");
            return await provider.AnalyzeVoiceWorkoutAsync(audioData, workoutType);
        }

        public async Task<VoiceFoodResponse> AnalyzeVoiceFoodAsync(byte[] audioData, string? mealType = null)
        {
            var provider = GetActiveProvider();
            _logger.LogInformation($"??? Using {provider.ProviderName} for voice food analysis");
            return await provider.AnalyzeVoiceFoodAsync(audioData, mealType);
        }

        public async Task<GeminiResponse> SendGeminiRequestAsync(List<GeminiContent> contents, GeminiGenerationConfig? config = null)
        {
            // Метод для обратной совместимости - можно убрать позже
            throw new NotImplementedException("Use specific analysis methods instead");
        }

        public string ConvertImageToBase64(byte[] imageData, string mimeType)
        {
            return Convert.ToBase64String(imageData);
        }

        public async Task<bool> ValidateImageQualityAsync(byte[] imageData)
        {
            // Та же логика из оригинального GeminiService
            try
            {
                if (imageData.Length < 10 * 1024) return false;
                if (imageData.Length > 20 * 1024 * 1024) return false;

                var imageHeaders = new[]
                {
                    new byte[] { 0xFF, 0xD8, 0xFF }, // JPEG
                    new byte[] { 0x89, 0x50, 0x4E, 0x47 }, // PNG
                    new byte[] { 0x47, 0x49, 0x46 }, // GIF
                };

                return imageHeaders.Any(header =>
                {
                    if (imageData.Length < header.Length) return false;
                    return header.SequenceEqual(imageData.Take(header.Length));
                });
            }
            catch
            {
                return false;
            }
        }

        public async Task<bool> IsHealthyAsync()
        {
            var provider = GetActiveProvider();
            return await provider.IsHealthyAsync();
        }

        private IAIProvider GetActiveProvider()
        {
            var activeProviderName = _configuration["AI:ActiveProvider"] ?? "Vertex AI (Gemini Pro 2.5)";

            if (_providers.TryGetValue(activeProviderName, out var provider))
            {
                return provider;
            }

            // Fallback к первому доступному провайдеру
            var fallbackProvider = _providers.Values.FirstOrDefault();
            if (fallbackProvider == null)
            {
                throw new InvalidOperationException("No AI providers available");
            }

            _logger.LogWarning($"?? Provider '{activeProviderName}' not found, using fallback: {fallbackProvider.ProviderName}");
            return fallbackProvider;
        }

        public async Task<Dictionary<string, bool>> GetProviderHealthStatusAsync()
        {
            var healthStatus = new Dictionary<string, bool>();

            foreach (var provider in _providers.Values)
            {
                try
                {
                    var isHealthy = await provider.IsHealthyAsync();
                    healthStatus[provider.ProviderName] = isHealthy;
                }
                catch
                {
                    healthStatus[provider.ProviderName] = false;
                }
            }

            return healthStatus;
        }
    }
}
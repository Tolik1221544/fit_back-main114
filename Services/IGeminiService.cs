using FitnessTracker.API.DTOs;

namespace FitnessTracker.API.Services
{
    public interface IGeminiService
    {
        Task<FoodScanResponse> AnalyzeFoodImageAsync(byte[] imageData, string? userPrompt = null);
        Task<BodyScanResponse> AnalyzeBodyImagesAsync(
            byte[]? frontImageData,
            byte[]? sideImageData,
            byte[]? backImageData,
            decimal? weight = null,
            decimal? height = null,
            int? age = null,
            string? gender = null,
            string? goals = null);
        Task<VoiceWorkoutResponse> AnalyzeVoiceWorkoutAsync(byte[] audioData, string? workoutType = null);
        Task<VoiceFoodResponse> AnalyzeVoiceFoodAsync(byte[] audioData, string? mealType = null);
        Task<TextWorkoutResponse> AnalyzeTextWorkoutAsync(string workoutText, string? workoutType = null);
        Task<TextFoodResponse> AnalyzeTextFoodAsync(string foodText, string? mealType = null);
        Task<FoodCorrectionResponse> CorrectFoodItemAsync(string originalFoodName, string correctionText);

        Task<bool> IsHealthyAsync();
        Task<Dictionary<string, bool>> GetProviderHealthStatusAsync();
        Task<bool> ValidateImageQualityAsync(byte[] imageData);
        string ConvertImageToBase64(byte[] imageData, string mimeType);

        Task<GeminiResponse> SendGeminiRequestAsync(List<GeminiContent> contents, GeminiGenerationConfig? config = null);
    }
}
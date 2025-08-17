using FitnessTracker.API.DTOs;

namespace FitnessTracker.API.Services.AI
{
    public interface IAIProvider
    {
        string ProviderName { get; }
        Task<FoodScanResponse> AnalyzeFoodImageAsync(byte[] imageData, string? userPrompt = null, string? locale = null);
        Task<BodyScanResponse> AnalyzeBodyImagesAsync(
            byte[]? frontImageData,
            byte[]? sideImageData,
            byte[]? backImageData,
            decimal? weight = null,
            decimal? height = null,
            int? age = null,
            string? gender = null,
            string? goals = null,
            string? locale = null);
        Task<VoiceWorkoutResponse> AnalyzeVoiceWorkoutAsync(byte[] audioData, string? workoutType = null, string? locale = null);
        Task<VoiceFoodResponse> AnalyzeVoiceFoodAsync(byte[] audioData, string? mealType = null, string? locale = null);
        Task<TextWorkoutResponse> AnalyzeTextWorkoutAsync(string workoutText, string? workoutType = null, string? locale = null);
        Task<TextFoodResponse> AnalyzeTextFoodAsync(string foodText, string? mealType = null, string? locale = null);
        Task<FoodCorrectionResponse> CorrectFoodItemAsync(string originalFoodName, string correctionText, string? locale = null);
        Task<bool> IsHealthyAsync();
    }
}
using FitnessTracker.API.DTOs;

namespace FitnessTracker.API.Services.AI
{
	public interface IAIProvider
	{
		string ProviderName { get; }
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
		Task<bool> IsHealthyAsync();
	}
}
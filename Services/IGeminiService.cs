using FitnessTracker.API.DTOs;

namespace FitnessTracker.API.Services
{
    public interface IGeminiService
    {
        /// <summary>
        /// 🍎 Анализ изображения еды с помощью Gemini
        /// </summary>
        Task<FoodScanResponse> AnalyzeFoodImageAsync(byte[] imageData, string? userPrompt = null);

        /// <summary>
        /// 💪 Анализ изображений тела с помощью Gemini
        /// </summary>
        Task<BodyScanResponse> AnalyzeBodyImagesAsync(
            byte[]? frontImageData,
            byte[]? sideImageData,
            byte[]? backImageData,
            decimal? weight = null,
            decimal? height = null,
            int? age = null,
            string? gender = null,
            string? goals = null);

        /// <summary>
        /// 🎤 Распознавание речи и анализ тренировки
        /// </summary>
        Task<VoiceWorkoutResponse> AnalyzeVoiceWorkoutAsync(byte[] audioData, string? workoutType = null);

        /// <summary>
        /// 🗣️ Распознавание речи и анализ питания
        /// </summary>
        Task<VoiceFoodResponse> AnalyzeVoiceFoodAsync(byte[] audioData, string? mealType = null);

        /// <summary>
        /// 🧠 Общий метод для отправки запроса к Gemini API
        /// </summary>
        Task<GeminiResponse> SendGeminiRequestAsync(List<GeminiContent> contents, GeminiGenerationConfig? config = null);

        /// <summary>
        /// 🔄 Конвертация изображения в base64 для Gemini
        /// </summary>
        string ConvertImageToBase64(byte[] imageData, string mimeType);

        /// <summary>
        /// 📝 Проверка качества изображения
        /// </summary>
        Task<bool> ValidateImageQualityAsync(byte[] imageData);

        /// <summary>
        /// 🏥 Проверка здоровья сервиса
        /// </summary>
        Task<bool> IsHealthyAsync();
    }
}
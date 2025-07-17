using FitnessTracker.API.DTOs;

namespace FitnessTracker.API.Services
{
    public interface IVoiceFileService
    {
        /// <summary>
        /// Сохранить голосовой файл на сервере
        /// </summary>
        Task<string> SaveVoiceFileAsync(IFormFile audioFile, string userId, string voiceType);

        /// <summary>
        /// Сохранить голосовой файл из байтов
        /// </summary>
        Task<string> SaveVoiceFileAsync(byte[] audioData, string fileName, string userId, string voiceType);

        /// <summary>
        /// Получить список голосовых файлов пользователя
        /// </summary>
        Task<IEnumerable<VoiceFileDto>> GetUserVoiceFilesAsync(string userId);

        /// <summary>
        /// Скачать голосовой файл
        /// </summary>
        Task<(byte[] data, string fileName, string contentType)?> DownloadVoiceFileAsync(string userId, string fileId);

        /// <summary>
        /// Удалить голосовой файл
        /// </summary>
        Task<bool> DeleteVoiceFileAsync(string userId, string fileId);

        /// <summary>
        /// Получить статистику голосовых файлов
        /// </summary>
        Task<VoiceFilesStatsDto> GetVoiceFilesStatsAsync(string userId);

        /// <summary>
        /// Очистить старые файлы (для фонового сервиса)
        /// </summary>
        Task<int> CleanupOldFilesAsync(TimeSpan maxAge);

        /// <summary>
        /// Получить URL для скачивания файла
        /// </summary>
        string GetDownloadUrl(string fileId);
    }
}
using FitnessTracker.API.DTOs;

namespace FitnessTracker.API.Services
{
    public interface IVoiceFileService
    {
        Task<string> SaveVoiceFileAsync(IFormFile audioFile, string userId, string voiceType);

        Task<string> SaveVoiceFileAsync(byte[] audioData, string fileName, string userId, string voiceType);

        Task<IEnumerable<VoiceFileDto>> GetUserVoiceFilesAsync(string userId);

        Task<IEnumerable<VoiceFileDto>> GetAllVoiceFilesAsync();

        Task<(byte[] data, string fileName, string contentType)?> DownloadVoiceFileAsync(string userId, string fileId);

        Task<(byte[] data, string fileName, string contentType)?> DownloadAnyVoiceFileAsync(string fileId);

        Task<bool> DeleteVoiceFileAsync(string userId, string fileId);

        Task<VoiceFilesStatsDto> GetVoiceFilesStatsAsync(string userId);

        Task<object> GetGlobalVoiceFilesStatsAsync();

        Task<int> CleanupOldFilesAsync(TimeSpan maxAge);

        string GetDownloadUrl(string fileId);
    }
}
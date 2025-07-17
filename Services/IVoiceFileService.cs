using FitnessTracker.API.DTOs;

namespace FitnessTracker.API.Services
{
    public interface IVoiceFileService
    {
        /// <summary>
        /// ��������� ��������� ���� �� �������
        /// </summary>
        Task<string> SaveVoiceFileAsync(IFormFile audioFile, string userId, string voiceType);

        /// <summary>
        /// ��������� ��������� ���� �� ������
        /// </summary>
        Task<string> SaveVoiceFileAsync(byte[] audioData, string fileName, string userId, string voiceType);

        /// <summary>
        /// �������� ������ ��������� ������ ������������
        /// </summary>
        Task<IEnumerable<VoiceFileDto>> GetUserVoiceFilesAsync(string userId);

        /// <summary>
        /// ������� ��������� ����
        /// </summary>
        Task<(byte[] data, string fileName, string contentType)?> DownloadVoiceFileAsync(string userId, string fileId);

        /// <summary>
        /// ������� ��������� ����
        /// </summary>
        Task<bool> DeleteVoiceFileAsync(string userId, string fileId);

        /// <summary>
        /// �������� ���������� ��������� ������
        /// </summary>
        Task<VoiceFilesStatsDto> GetVoiceFilesStatsAsync(string userId);

        /// <summary>
        /// �������� ������ ����� (��� �������� �������)
        /// </summary>
        Task<int> CleanupOldFilesAsync(TimeSpan maxAge);

        /// <summary>
        /// �������� URL ��� ���������� �����
        /// </summary>
        string GetDownloadUrl(string fileId);
    }
}
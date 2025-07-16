using Microsoft.AspNetCore.Hosting;

namespace FitnessTracker.API.Services
{
    public interface IAudioFileService
    {
        Task<AudioFileResult> SaveAudioFileAsync(IFormFile audioFile, string userId, int? expirationHours = 24);
        Task<AudioFileResult> SaveAudioFileAsync(byte[] audioData, string fileName, string userId, int? expirationHours = 24);
        Task<byte[]?> GetAudioFileAsync(string fileId);
        Task<bool> DeleteAudioFileAsync(string fileId);
        Task<AudioFileInfo?> GetAudioFileInfoAsync(string fileId);
        Task<int> CleanupExpiredFilesAsync();
        Task<List<AudioFileInfo>> GetUserAudioFilesAsync(string userId);
        Task<List<AudioFileInfo>> GetAllAudioFilesAsync(bool includeExpired = false); // НОВОЕ
    }

    public class AudioFileService : IAudioFileService
    {
        private readonly IWebHostEnvironment _environment;
        private readonly ILogger<AudioFileService> _logger;
        private readonly IConfiguration _configuration;
        private static readonly Dictionary<string, AudioFileInfo> _audioFiles = new();
        private static readonly object _lockObject = new object();

        public AudioFileService(
            IWebHostEnvironment environment,
            ILogger<AudioFileService> logger,
            IConfiguration configuration)
        {
            _environment = environment;
            _logger = logger;
            _configuration = configuration;
        }

        public async Task<AudioFileResult> SaveAudioFileAsync(IFormFile audioFile, string userId, int? expirationHours = 24)
        {
            try
            {
                if (audioFile == null || audioFile.Length == 0)
                    return AudioFileResult.CreateError("Audio file is required");

                // Проверяем размер файла (максимум 50MB)
                var maxSizeBytes = _configuration.GetValue<long>("AudioFiles:MaxSizeMB", 50) * 1024 * 1024;
                if (audioFile.Length > maxSizeBytes)
                    return AudioFileResult.CreateError($"Audio file size must be less than {maxSizeBytes / 1024 / 1024}MB");

                // Проверяем тип файла
                var allowedTypes = new[] {
                    "audio/wav", "audio/wave", "audio/x-wav",
                    "audio/mp3", "audio/mpeg", "audio/mp4",
                    "audio/ogg", "audio/webm", "audio/3gpp",
                    "audio/aac", "audio/m4a"
                };

                if (!allowedTypes.Contains(audioFile.ContentType?.ToLowerInvariant()))
                {
                    _logger.LogWarning($"Unsupported audio type: {audioFile.ContentType}");
                    // Все равно пробуем сохранить, но логируем предупреждение
                }

                // Конвертируем в байты
                using var memoryStream = new MemoryStream();
                await audioFile.CopyToAsync(memoryStream);
                var audioData = memoryStream.ToArray();

                return await SaveAudioFileAsync(audioData, audioFile.FileName, userId, expirationHours);
            }
            catch (Exception ex)
            {
                _logger.LogError($"❌ Error saving audio file: {ex.Message}");
                return AudioFileResult.CreateError($"Failed to save audio file: {ex.Message}");
            }
        }

        public async Task<AudioFileResult> SaveAudioFileAsync(byte[] audioData, string fileName, string userId, int? expirationHours = 24)
        {
            try
            {
                if (audioData == null || audioData.Length == 0)
                    return AudioFileResult.CreateError("Audio data is required");

                // Создаем папку для аудио файлов
                var audioDir = GetAudioDirectory();
                Directory.CreateDirectory(audioDir);

                // Генерируем уникальное имя файла
                var fileId = Guid.NewGuid().ToString("N")[..12]; // 12 символов для читаемости
                var extension = Path.GetExtension(fileName)?.ToLowerInvariant();
                if (string.IsNullOrEmpty(extension))
                {
                    extension = DetectAudioExtension(audioData);
                }

                var uniqueFileName = $"{fileId}_{DateTime.UtcNow:yyyyMMdd_HHmmss}{extension}";
                var filePath = Path.Combine(audioDir, uniqueFileName);

                // Сохраняем файл
                await File.WriteAllBytesAsync(filePath, audioData);

                // Создаем информацию о файле
                var audioFileInfo = new AudioFileInfo
                {
                    FileId = fileId,
                    FileName = uniqueFileName,
                    OriginalName = fileName,
                    FilePath = filePath,
                    UserId = userId,
                    FileSize = audioData.Length,
                    MimeType = DetectMimeType(audioData),
                    CreatedAt = DateTime.UtcNow,
                    ExpiresAt = DateTime.UtcNow.AddHours(expirationHours ?? 24)
                };

                // Сохраняем в память
                lock (_lockObject)
                {
                    _audioFiles[fileId] = audioFileInfo;
                }

                _logger.LogInformation($"🎤 Audio file saved: {fileId} for user {userId}, size: {audioData.Length} bytes");

                return AudioFileResult.CreateSuccess(audioFileInfo);
            }
            catch (Exception ex)
            {
                _logger.LogError($"❌ Error saving audio data: {ex.Message}");
                return AudioFileResult.CreateError($"Failed to save audio data: {ex.Message}");
            }
        }

        public async Task<byte[]?> GetAudioFileAsync(string fileId)
        {
            try
            {
                lock (_lockObject)
                {
                    if (!_audioFiles.TryGetValue(fileId, out var audioInfo))
                        return null;

                    if (audioInfo.ExpiresAt < DateTime.UtcNow)
                    {
                        // Файл истек, удаляем
                        _audioFiles.Remove(fileId);
                        TryDeletePhysicalFile(audioInfo.FilePath);
                        return null;
                    }
                }

                var audioInfo2 = _audioFiles[fileId];
                if (!File.Exists(audioInfo2.FilePath))
                    return null;

                return await File.ReadAllBytesAsync(audioInfo2.FilePath);
            }
            catch (Exception ex)
            {
                _logger.LogError($"❌ Error reading audio file {fileId}: {ex.Message}");
                return null;
            }
        }

        public async Task<bool> DeleteAudioFileAsync(string fileId)
        {
            try
            {
                AudioFileInfo? audioInfo = null;

                lock (_lockObject)
                {
                    if (_audioFiles.TryGetValue(fileId, out audioInfo))
                    {
                        _audioFiles.Remove(fileId);
                    }
                }

                if (audioInfo != null)
                {
                    TryDeletePhysicalFile(audioInfo.FilePath);
                    _logger.LogInformation($"🗑️ Deleted audio file: {fileId}");
                    return true;
                }

                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError($"❌ Error deleting audio file {fileId}: {ex.Message}");
                return false;
            }
        }

        public Task<AudioFileInfo?> GetAudioFileInfoAsync(string fileId)
        {
            lock (_lockObject)
            {
                if (_audioFiles.TryGetValue(fileId, out var audioInfo))
                {
                    if (audioInfo.ExpiresAt >= DateTime.UtcNow)
                    {
                        return Task.FromResult<AudioFileInfo?>(audioInfo);
                    }
                    else
                    {
                        // Файл истек
                        _audioFiles.Remove(fileId);
                        TryDeletePhysicalFile(audioInfo.FilePath);
                    }
                }
                return Task.FromResult<AudioFileInfo?>(null);
            }
        }

        public async Task<int> CleanupExpiredFilesAsync()
        {
            var expiredFiles = new List<KeyValuePair<string, AudioFileInfo>>();

            lock (_lockObject)
            {
                expiredFiles = _audioFiles.Where(kvp => kvp.Value.ExpiresAt < DateTime.UtcNow).ToList();

                foreach (var (fileId, _) in expiredFiles)
                {
                    _audioFiles.Remove(fileId);
                }
            }

            var deletedCount = 0;
            foreach (var (_, audioInfo) in expiredFiles)
            {
                if (TryDeletePhysicalFile(audioInfo.FilePath))
                    deletedCount++;
            }

            if (deletedCount > 0)
            {
                _logger.LogInformation($"🧹 Cleaned up {deletedCount} expired audio files");
            }

            return deletedCount;
        }

        public Task<List<AudioFileInfo>> GetUserAudioFilesAsync(string userId)
        {
            lock (_lockObject)
            {
                var userFiles = _audioFiles.Values
                    .Where(f => f.UserId == userId && f.ExpiresAt >= DateTime.UtcNow)
                    .OrderByDescending(f => f.CreatedAt)
                    .ToList();

                return Task.FromResult(userFiles);
            }
        }

        /// <summary>
        /// 📋 Получить ВСЕ аудио файлы на сервере (для любого пользователя)
        /// </summary>
        /// <param name="includeExpired">Включить истекшие файлы</param>
        /// <returns>Список всех аудио файлов</returns>
        public Task<List<AudioFileInfo>> GetAllAudioFilesAsync(bool includeExpired = false)
        {
            lock (_lockObject)
            {
                var allFiles = _audioFiles.Values.AsEnumerable();

                if (!includeExpired)
                {
                    allFiles = allFiles.Where(f => f.ExpiresAt >= DateTime.UtcNow);
                }

                var result = allFiles
                    .OrderByDescending(f => f.CreatedAt)
                    .ToList();

                return Task.FromResult(result);
            }
        }

        // Helper methods
        private string GetAudioDirectory()
        {
            var webRootPath = _environment.WebRootPath ?? _environment.ContentRootPath;
            return Path.Combine(webRootPath, "audio-files");
        }

        private bool TryDeletePhysicalFile(string filePath)
        {
            try
            {
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                    return true;
                }
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError($"❌ Error deleting physical file {filePath}: {ex.Message}");
                return false;
            }
        }

        private string DetectAudioExtension(byte[] audioData)
        {
            if (audioData.Length >= 4)
            {
                // WAV
                if (audioData[0] == 0x52 && audioData[1] == 0x49 && audioData[2] == 0x46 && audioData[3] == 0x46)
                    return ".wav";

                // MP3
                if (audioData.Length >= 3 && audioData[0] == 0xFF && (audioData[1] & 0xE0) == 0xE0)
                    return ".mp3";

                // OGG
                if (audioData[0] == 0x4F && audioData[1] == 0x67 && audioData[2] == 0x67 && audioData[3] == 0x53)
                    return ".ogg";
            }

            return ".audio"; // Общее расширение
        }

        private string DetectMimeType(byte[] audioData)
        {
            if (audioData.Length >= 4)
            {
                // WAV
                if (audioData[0] == 0x52 && audioData[1] == 0x49 && audioData[2] == 0x46 && audioData[3] == 0x46)
                    return "audio/wav";

                // MP3
                if (audioData.Length >= 3 && audioData[0] == 0xFF && (audioData[1] & 0xE0) == 0xE0)
                    return "audio/mp3";

                // OGG
                if (audioData[0] == 0x4F && audioData[1] == 0x67 && audioData[2] == 0x67 && audioData[3] == 0x53)
                    return "audio/ogg";
            }

            return "audio/unknown";
        }
    }

    // Модели данных
    public class AudioFileInfo
    {
        public string FileId { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
        public string OriginalName { get; set; } = string.Empty;
        public string FilePath { get; set; } = string.Empty;
        public string UserId { get; set; } = string.Empty;
        public long FileSize { get; set; }
        public string MimeType { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime ExpiresAt { get; set; }
    }

    public class AudioFileResult
    {
        public bool IsSuccess { get; set; }
        public string? ErrorMessage { get; set; }
        public AudioFileInfo? FileInfo { get; set; }

        public static AudioFileResult CreateSuccess(AudioFileInfo fileInfo) => new() { IsSuccess = true, FileInfo = fileInfo };
        public static AudioFileResult CreateError(string errorMessage) => new() { IsSuccess = false, ErrorMessage = errorMessage };
    }
}
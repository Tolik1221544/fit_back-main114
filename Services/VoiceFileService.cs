using FitnessTracker.API.DTOs;

namespace FitnessTracker.API.Services
{
    public class VoiceFileService : IVoiceFileService
    {
        private readonly IWebHostEnvironment _environment;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly IConfiguration _configuration;
        private readonly ILogger<VoiceFileService> _logger;

        public VoiceFileService(
            IWebHostEnvironment environment,
            IHttpContextAccessor httpContextAccessor,
            IConfiguration configuration,
            ILogger<VoiceFileService> logger)
        {
            _environment = environment;
            _httpContextAccessor = httpContextAccessor;
            _configuration = configuration;
            _logger = logger;
        }

        public async Task<string> SaveVoiceFileAsync(IFormFile audioFile, string userId, string voiceType)
        {
            try
            {
                if (audioFile == null || audioFile.Length == 0)
                    throw new ArgumentException("Audio file is required");

                // Проверяем размер файла (максимум 50MB для аудио)
                if (audioFile.Length > 50 * 1024 * 1024)
                    throw new ArgumentException("Audio file size must be less than 50MB");

                // Проверяем тип файла
                var allowedTypes = new[] { "audio/wav", "audio/mpeg", "audio/mp3", "audio/ogg", "audio/webm", "audio/m4a" };
                if (!allowedTypes.Contains(audioFile.ContentType.ToLowerInvariant()))
                {
                    _logger.LogWarning($"Unsupported audio type: {audioFile.ContentType}");
                    // Разрешаем любые файлы, но логируем предупреждение
                }

                // Создаем уникальное имя файла
                var fileId = Guid.NewGuid().ToString("N")[..12]; // 12 символов для ID
                var timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
                var fileExtension = GetAudioExtension(audioFile.FileName, audioFile.ContentType);
                var fileName = $"{userId}_{voiceType}_{fileId}_{timestamp}{fileExtension}";

                // Определяем папку
                var folderName = voiceType == "workout" ? "voice-workouts" : "voice-food";
                var uploadsPath = GetUploadsPath();
                var folderPath = Path.Combine(uploadsPath, folderName);

                // Создаем папку если не существует
                Directory.CreateDirectory(folderPath);

                var filePath = Path.Combine(folderPath, fileName);

                // Сохраняем файл
                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await audioFile.CopyToAsync(stream);
                }

                _logger.LogInformation($"✅ Voice file saved: {fileName} (Size: {audioFile.Length / 1024.0:F1} KB)");

                return fileId; // Возвращаем ID для получения файла
            }
            catch (Exception ex)
            {
                _logger.LogError($"❌ Error saving voice file: {ex.Message}");
                throw;
            }
        }

        public async Task<string> SaveVoiceFileAsync(byte[] audioData, string fileName, string userId, string voiceType)
        {
            try
            {
                if (audioData == null || audioData.Length == 0)
                    throw new ArgumentException("Audio data is required");

                if (audioData.Length > 50 * 1024 * 1024)
                    throw new ArgumentException("Audio data size must be less than 50MB");

                var fileId = Guid.NewGuid().ToString("N")[..12];
                var timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
                var fileExtension = Path.GetExtension(fileName);
                if (string.IsNullOrEmpty(fileExtension))
                    fileExtension = ".wav";

                var fullFileName = $"{userId}_{voiceType}_{fileId}_{timestamp}{fileExtension}";

                var folderName = voiceType == "workout" ? "voice-workouts" : "voice-food";
                var uploadsPath = GetUploadsPath();
                var folderPath = Path.Combine(uploadsPath, folderName);

                Directory.CreateDirectory(folderPath);

                var filePath = Path.Combine(folderPath, fullFileName);

                await File.WriteAllBytesAsync(filePath, audioData);

                _logger.LogInformation($"✅ Voice file saved from bytes: {fullFileName} (Size: {audioData.Length / 1024.0:F1} KB)");

                return fileId;
            }
            catch (Exception ex)
            {
                _logger.LogError($"❌ Error saving voice file from bytes: {ex.Message}");
                throw;
            }
        }

        public async Task<IEnumerable<VoiceFileDto>> GetUserVoiceFilesAsync(string userId)
        {
            try
            {
                var files = new List<VoiceFileDto>();

                // Получаем файлы из обеих папок
                var workoutFiles = await GetFilesFromFolder("voice-workouts", userId, "workout");
                var foodFiles = await GetFilesFromFolder("voice-food", userId, "food");

                files.AddRange(workoutFiles);
                files.AddRange(foodFiles);

                return files.OrderByDescending(f => f.CreatedAt);
            }
            catch (Exception ex)
            {
                _logger.LogError($"❌ Error getting voice files for user {userId}: {ex.Message}");
                return new List<VoiceFileDto>();
            }
        }

        public async Task<(byte[] data, string fileName, string contentType)?> DownloadVoiceFileAsync(string userId, string fileId)
        {
            try
            {
                // Ищем файл в обеих папках
                var filePath = FindUserFile(userId, fileId);

                if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
                {
                    _logger.LogWarning($"Voice file not found: {fileId} for user {userId}");
                    return null;
                }

                var fileBytes = await File.ReadAllBytesAsync(filePath);
                var fileName = Path.GetFileName(filePath);
                var contentType = GetAudioContentType(fileName);

                _logger.LogInformation($"📥 Downloaded voice file: {fileName} by {userId}");

                return (fileBytes, fileName, contentType);
            }
            catch (Exception ex)
            {
                _logger.LogError($"❌ Error downloading voice file {fileId} for user {userId}: {ex.Message}");
                return null;
            }
        }

        public async Task<bool> DeleteVoiceFileAsync(string userId, string fileId)
        {
            try
            {
                var filePath = FindUserFile(userId, fileId);

                if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
                {
                    _logger.LogWarning($"Voice file not found for deletion: {fileId} for user {userId}");
                    return false;
                }

                File.Delete(filePath);
                _logger.LogInformation($"🗑️ Deleted voice file: {Path.GetFileName(filePath)} by {userId}");

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError($"❌ Error deleting voice file {fileId} for user {userId}: {ex.Message}");
                return false;
            }
        }

        public async Task<VoiceFilesStatsDto> GetVoiceFilesStatsAsync(string userId)
        {
            try
            {
                var allFiles = await GetUserVoiceFilesAsync(userId);
                var workoutFiles = allFiles.Where(f => f.VoiceType == "workout").ToList();
                var foodFiles = allFiles.Where(f => f.VoiceType == "food").ToList();

                var today = DateTime.UtcNow.Date;
                var startOfMonth = new DateTime(today.Year, today.Month, 1);

                return new VoiceFilesStatsDto
                {
                    TotalFiles = allFiles.Count(),
                    WorkoutFiles = workoutFiles.Count,
                    FoodFiles = foodFiles.Count,
                    TotalSizeMB = Math.Round(allFiles.Sum(f => f.SizeMB), 2),
                    AverageFileSizeMB = allFiles.Any() ? Math.Round(allFiles.Average(f => f.SizeMB), 2) : 0,
                    OldestFileDate = allFiles.Any() ? allFiles.Min(f => f.CreatedAt) : null,
                    NewestFileDate = allFiles.Any() ? allFiles.Max(f => f.CreatedAt) : null,
                    FilesThisMonth = allFiles.Count(f => f.CreatedAt >= startOfMonth),
                    FilesToday = allFiles.Count(f => f.CreatedAt.Date == today)
                };
            }
            catch (Exception ex)
            {
                _logger.LogError($"❌ Error getting voice files stats for user {userId}: {ex.Message}");
                return new VoiceFilesStatsDto();
            }
        }

        public async Task<int> CleanupOldFilesAsync(TimeSpan maxAge)
        {
            try
            {
                var deletedCount = 0;
                var cutoffDate = DateTime.UtcNow - maxAge;

                var folders = new[] { "voice-workouts", "voice-food" };

                foreach (var folder in folders)
                {
                    var folderPath = Path.Combine(GetUploadsPath(), folder);
                    if (!Directory.Exists(folderPath))
                        continue;

                    var files = Directory.GetFiles(folderPath, "*.*");

                    foreach (var filePath in files)
                    {
                        try
                        {
                            var fileInfo = new FileInfo(filePath);
                            if (fileInfo.CreationTime < cutoffDate)
                            {
                                File.Delete(filePath);
                                deletedCount++;
                                _logger.LogDebug($"🧹 Cleaned up old voice file: {fileInfo.Name}");
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError($"❌ Error deleting old file {filePath}: {ex.Message}");
                        }
                    }
                }

                if (deletedCount > 0)
                {
                    _logger.LogInformation($"🧹 Cleaned up {deletedCount} old voice files");
                }

                return deletedCount;
            }
            catch (Exception ex)
            {
                _logger.LogError($"❌ Error during cleanup: {ex.Message}");
                return 0;
            }
        }

        public string GetDownloadUrl(string fileId)
        {
            var baseUrl = GetBaseUrl();
            return $"{baseUrl}/api/voice-files/download/{fileId}";
        }

        // Helper methods
        private async Task<List<VoiceFileDto>> GetFilesFromFolder(string folderName, string userId, string voiceType)
        {
            var files = new List<VoiceFileDto>();
            var folderPath = Path.Combine(GetUploadsPath(), folderName);

            if (!Directory.Exists(folderPath))
                return files;

            var allFiles = Directory.GetFiles(folderPath, $"{userId}_*.*");

            foreach (var filePath in allFiles)
            {
                try
                {
                    var fileInfo = new FileInfo(filePath);
                    var fileName = Path.GetFileNameWithoutExtension(fileInfo.Name);
                    var fileId = ExtractFileId(fileName);

                    files.Add(new VoiceFileDto
                    {
                        FileId = fileId,
                        FileName = fileInfo.Name,
                        OriginalFileName = fileInfo.Name,
                        VoiceType = voiceType,
                        SizeBytes = fileInfo.Length,
                        SizeMB = Math.Round(fileInfo.Length / (1024.0 * 1024.0), 2),
                        CreatedAt = fileInfo.CreationTime,
                        Extension = fileInfo.Extension,
                        DownloadUrl = GetDownloadUrl(fileId),
                        ContentType = GetAudioContentType(fileInfo.Name)
                    });
                }
                catch (Exception ex)
                {
                    _logger.LogError($"❌ Error processing file {filePath}: {ex.Message}");
                }
            }

            return files;
        }

        private string? FindUserFile(string userId, string fileId)
        {
            var folders = new[] { "voice-workouts", "voice-food" };

            foreach (var folder in folders)
            {
                var folderPath = Path.Combine(GetUploadsPath(), folder);
                if (!Directory.Exists(folderPath))
                    continue;

                var files = Directory.GetFiles(folderPath, $"{userId}_*{fileId}*.*");
                if (files.Any())
                    return files.First();
            }

            return null;
        }

        private string ExtractFileId(string fileName)
        {
            // Формат: userId_voiceType_fileId_timestamp
            var parts = fileName.Split('_');
            if (parts.Length >= 3)
                return parts[2]; // fileId

            return fileName.Substring(0, Math.Min(12, fileName.Length));
        }

        private string GetAudioExtension(string fileName, string contentType)
        {
            var extension = Path.GetExtension(fileName);
            if (!string.IsNullOrEmpty(extension))
                return extension;

            return contentType.ToLowerInvariant() switch
            {
                "audio/wav" => ".wav",
                "audio/mpeg" or "audio/mp3" => ".mp3",
                "audio/ogg" => ".ogg",
                "audio/webm" => ".webm",
                "audio/m4a" => ".m4a",
                _ => ".wav"
            };
        }

        private string GetAudioContentType(string fileName)
        {
            var extension = Path.GetExtension(fileName).ToLowerInvariant();
            return extension switch
            {
                ".wav" => "audio/wav",
                ".mp3" => "audio/mpeg",
                ".ogg" => "audio/ogg",
                ".webm" => "audio/webm",
                ".m4a" => "audio/mp4",
                _ => "application/octet-stream"
            };
        }

        private string GetUploadsPath()
        {
            var webRootPath = _environment.WebRootPath;
            if (string.IsNullOrEmpty(webRootPath))
                webRootPath = _environment.ContentRootPath;

            return Path.Combine(webRootPath, "uploads");
        }

        private string GetBaseUrl()
        {
            var configBaseUrl = _configuration["BaseUrl"];
            if (!string.IsNullOrEmpty(configBaseUrl))
                return configBaseUrl;

            var httpContext = _httpContextAccessor.HttpContext;
            if (httpContext != null)
            {
                var request = httpContext.Request;
                return $"{request.Scheme}://{request.Host}";
            }

            return "http://178.236.16.91:60170";
        }
    }
}
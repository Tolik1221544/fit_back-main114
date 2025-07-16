namespace FitnessTracker.API.Services
{
    public class AudioTestCleanupService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<AudioTestCleanupService> _logger;

        public AudioTestCleanupService(IServiceProvider serviceProvider, ILogger<AudioTestCleanupService> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("🧹 Audio cleanup service started");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    // Вызываем очистку через HTTP запрос к контроллеру
                    using var scope = _serviceProvider.CreateScope();
                    var httpContextAccessor = scope.ServiceProvider.GetService<IHttpContextAccessor>();

                    // Альтернативно - прямой доступ к папке
                    await CleanupTestAudioDirectoryAsync();

                    // Проверяем каждые 30 минут
                    await Task.Delay(TimeSpan.FromMinutes(30), stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError($"❌ Error in audio cleanup service: {ex.Message}");
                    await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
                }
            }
        }

        private async Task CleanupTestAudioDirectoryAsync()
        {
            try
            {
                var testAudioDir = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "test-audio");

                if (!Directory.Exists(testAudioDir))
                    return;

                // Используем Task.Run для CPU-интенсивной операции
                var deletedCount = await Task.Run(() =>
                {
                    var files = Directory.GetFiles(testAudioDir);
                    var deleted = 0;

                    foreach (var file in files)
                    {
                        var fileInfo = new FileInfo(file);

                        // Удаляем файлы старше 1 часа
                        if (fileInfo.CreationTime < DateTime.UtcNow.AddHours(-1))
                        {
                            try
                            {
                                File.Delete(file);
                                deleted++;
                                _logger.LogDebug($"🗑️ Deleted expired test audio: {fileInfo.Name}");
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError($"❌ Error deleting {fileInfo.Name}: {ex.Message}");
                            }
                        }
                    }

                    return deleted;
                });

                if (deletedCount > 0)
                {
                    _logger.LogInformation($"🧹 Cleaned up {deletedCount} expired test audio files");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"❌ Error in cleanup directory: {ex.Message}");
            }
        }
    }
}
using Google.Apis.Auth.OAuth2;
using System.Text.Json;

namespace FitnessTracker.API.Services.AI
{
    public class GoogleCloudTokenService : IGoogleCloudTokenService
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<GoogleCloudTokenService> _logger;
        private GoogleCredential? _credential;
        private readonly object _lock = new object();

        public GoogleCloudTokenService(IConfiguration configuration, ILogger<GoogleCloudTokenService> logger)
        {
            _configuration = configuration;
            _logger = logger;
        }

        public async Task<string> GetAccessTokenAsync()
        {
            try
            {
                if (_credential == null)
                {
                    await InitializeCredentialAsync();
                }

                var accessToken = await _credential!.UnderlyingCredential.GetAccessTokenForRequestAsync();
                _logger.LogInformation("🔑 Google Cloud access token obtained successfully");
                return accessToken;
            }
            catch (Exception ex)
            {
                _logger.LogError($"❌ Failed to get Google Cloud access token: {ex.Message}");
                throw;
            }
        }

        public async Task<bool> ValidateServiceAccountAsync()
        {
            try
            {
                await GetAccessTokenAsync();
                _logger.LogInformation("✅ Service account validation successful");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError($"❌ Service account validation failed: {ex.Message}");
                return false;
            }
        }

        private async Task InitializeCredentialAsync()
        {
            lock (_lock)
            {
                if (_credential != null) return;

                try
                {
                    // Путь к файлу сервисного аккаунта
                    var serviceAccountPath = _configuration["GoogleCloud:ServiceAccountPath"];

                    if (!string.IsNullOrEmpty(serviceAccountPath) && File.Exists(serviceAccountPath))
                    {
                        _logger.LogInformation($"📁 Loading service account from file: {serviceAccountPath}");
                        _credential = GoogleCredential.FromFile(serviceAccountPath)
                            .CreateScoped("https://www.googleapis.com/auth/cloud-platform");
                    }
                    else
                    {
                        // Попытка получить из переменных окружения или metadata сервера
                        _logger.LogInformation("🔍 Attempting to get default credentials");
                        _credential = GoogleCredential.GetApplicationDefault()
                            .CreateScoped("https://www.googleapis.com/auth/cloud-platform");
                    }

                    _logger.LogInformation("✅ Google Cloud credentials initialized successfully");
                }
                catch (Exception ex)
                {
                    _logger.LogError($"❌ Failed to initialize Google Cloud credentials: {ex.Message}");
                    throw;
                }
            }
        }
    }
}
using Microsoft.AspNetCore.Hosting;

namespace FitnessTracker.API.Services
{
    public class ImageService : IImageService
    {
        private readonly IWebHostEnvironment _environment;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly IConfiguration _configuration;
        private readonly ILogger<ImageService> _logger;

        public ImageService(
            IWebHostEnvironment environment,
            IHttpContextAccessor httpContextAccessor,
            IConfiguration configuration,
            ILogger<ImageService> logger)
        {
            _environment = environment;
            _httpContextAccessor = httpContextAccessor;
            _configuration = configuration;
            _logger = logger;
        }

        public async Task<string> SaveImageAsync(IFormFile image, string folder)
        {
            try
            {
                if (image == null || image.Length == 0)
                    throw new ArgumentException("Image is required");

                if (image.Length > 10 * 1024 * 1024)
                    throw new ArgumentException("Image size must be less than 10MB");

                var allowedTypes = new[] { "image/jpeg", "image/jpg", "image/png", "image/gif" };
                if (!allowedTypes.Contains(image.ContentType.ToLowerInvariant()))
                    throw new ArgumentException("Only JPEG, PNG and GIF images are allowed");

                var fileExtension = Path.GetExtension(image.FileName).ToLowerInvariant();
                var fileName = $"{Guid.NewGuid()}{fileExtension}";

                var uploadsPath = GetUploadsPath();
                var folderPath = Path.Combine(uploadsPath, folder);

                Directory.CreateDirectory(folderPath);

                var filePath = Path.Combine(folderPath, fileName);

                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await image.CopyToAsync(stream);
                }

                var imageUrl = GetImageUrl(fileName, folder);
                _logger.LogInformation($"✅ Image saved: {imageUrl}");

                return imageUrl;
            }
            catch (Exception ex)
            {
                _logger.LogError($"❌ Error saving image: {ex.Message}");
                throw;
            }
        }

        public async Task<string> SaveImageAsync(byte[] imageData, string fileName, string folder)
        {
            try
            {
                if (imageData == null || imageData.Length == 0)
                    throw new ArgumentException("Image data is required");

                if (imageData.Length > 10 * 1024 * 1024)
                    throw new ArgumentException("Image size must be less than 10MB");

                var uniqueFileName = $"{Guid.NewGuid()}_{fileName}";

                var uploadsPath = GetUploadsPath();
                var folderPath = Path.Combine(uploadsPath, folder);
                Directory.CreateDirectory(folderPath);

                var filePath = Path.Combine(folderPath, uniqueFileName);

                await File.WriteAllBytesAsync(filePath, imageData);

                var imageUrl = GetImageUrl(uniqueFileName, folder);
                _logger.LogInformation($"✅ Image saved from bytes: {imageUrl}");

                return imageUrl;
            }
            catch (Exception ex)
            {
                _logger.LogError($"❌ Error saving image from bytes: {ex.Message}");
                throw;
            }
        }

        public Task<bool> DeleteImageAsync(string imageUrl)
        {
            try
            {
                if (string.IsNullOrEmpty(imageUrl))
                    return Task.FromResult(false);

                var uri = new Uri(imageUrl, UriKind.RelativeOrAbsolute);
                var relativePath = uri.IsAbsoluteUri ? uri.LocalPath : imageUrl;

                if (relativePath.StartsWith("/uploads/"))
                    relativePath = relativePath.Substring("/uploads/".Length);

                var uploadsPath = GetUploadsPath();
                var filePath = Path.Combine(uploadsPath, relativePath);

                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                    _logger.LogInformation($"✅ Image deleted: {filePath}");
                    return Task.FromResult(true);
                }

                _logger.LogWarning($"⚠️ Image not found for deletion: {filePath}");
                return Task.FromResult(false);
            }
            catch (Exception ex)
            {
                _logger.LogError($"❌ Error deleting image: {ex.Message}");
                return Task.FromResult(false);
            }
        }


        public string GetImageUrl(string fileName, string folder)
        {
            // Получаем базовый URL из конфигурации или текущего запроса
            var baseUrl = GetBaseUrl();
            return $"{baseUrl}/uploads/{folder}/{fileName}";
        }

        private string GetBaseUrl()
        {
            // Сначала пытаемся получить из конфигурации
            var configBaseUrl = _configuration["BaseUrl"];
            if (!string.IsNullOrEmpty(configBaseUrl))
                return configBaseUrl;

            // Если нет в конфигурации, строим из текущего запроса
            var httpContext = _httpContextAccessor.HttpContext;
            if (httpContext != null)
            {
                var request = httpContext.Request;
                return $"{request.Scheme}://{request.Host}";
            }

            // Fallback
            return "http://178.236.16.91:60170";
        }

        private string GetUploadsPath()
        {
            var webRootPath = _environment.WebRootPath;
            if (string.IsNullOrEmpty(webRootPath))
                webRootPath = _environment.ContentRootPath;

            return Path.Combine(webRootPath, "uploads");
        }
    }
}
namespace FitnessTracker.API.Services
{
    public class ImageService : IImageService
    {
        private readonly IWebHostEnvironment _environment;
        private readonly IConfiguration _configuration;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly ILogger<ImageService> _logger;

        public ImageService(
            IWebHostEnvironment environment,
            IConfiguration configuration,
            IHttpContextAccessor httpContextAccessor,
            ILogger<ImageService> logger)
        {
            _environment = environment;
            _configuration = configuration;
            _httpContextAccessor = httpContextAccessor;
            _logger = logger;
        }

        public async Task<string> SaveImageAsync(IFormFile image, string folder)
        {
            try
            {
                if (image == null || image.Length == 0)
                    throw new ArgumentException("Invalid image file");

                // Проверяем тип файла
                var allowedTypes = new[] { "image/jpeg", "image/jpg", "image/png", "image/webp" };
                if (!allowedTypes.Contains(image.ContentType.ToLowerInvariant()))
                    throw new ArgumentException("Invalid image type. Only JPEG, PNG and WebP are allowed.");

                // Создаем уникальное имя файла
                var extension = Path.GetExtension(image.FileName).ToLowerInvariant();
                if (string.IsNullOrEmpty(extension))
                    extension = ".jpg";

                var fileName = $"{Guid.NewGuid()}{extension}";
                var uploadsFolder = Path.Combine(_environment.WebRootPath ?? _environment.ContentRootPath, "uploads", folder);

                // Создаем папку если не существует
                Directory.CreateDirectory(uploadsFolder);

                var filePath = Path.Combine(uploadsFolder, fileName);

                // Сохраняем файл
                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await image.CopyToAsync(stream);
                }

                _logger.LogInformation($"Image saved: {fileName} in folder {folder}");

                // Возвращаем URL
                return GetImageUrl(fileName, folder);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error saving image: {ex.Message}");
                throw;
            }
        }

        public async Task<string> SaveImageAsync(byte[] imageData, string fileName, string folder)
        {
            try
            {
                if (imageData == null || imageData.Length == 0)
                    throw new ArgumentException("Invalid image data");

                // Создаем уникальное имя файла
                var extension = Path.GetExtension(fileName).ToLowerInvariant();
                if (string.IsNullOrEmpty(extension))
                    extension = ".jpg";

                var uniqueFileName = $"{Guid.NewGuid()}{extension}";
                var uploadsFolder = Path.Combine(_environment.WebRootPath ?? _environment.ContentRootPath, "uploads", folder);

                // Создаем папку если не существует
                Directory.CreateDirectory(uploadsFolder);

                var filePath = Path.Combine(uploadsFolder, uniqueFileName);

                // Сохраняем файл
                await File.WriteAllBytesAsync(filePath, imageData);

                _logger.LogInformation($"Image saved from bytes: {uniqueFileName} in folder {folder}");

                // Возвращаем URL
                return GetImageUrl(uniqueFileName, folder);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error saving image from bytes: {ex.Message}");
                throw;
            }
        }

        public async Task<bool> DeleteImageAsync(string imageUrl)
        {
            try
            {
                if (string.IsNullOrEmpty(imageUrl))
                    return false;

                // Извлекаем путь к файлу из URL
                var uri = new Uri(imageUrl, UriKind.RelativeOrAbsolute);
                var relativePath = uri.IsAbsoluteUri ? uri.LocalPath : imageUrl;

                // Удаляем /uploads/ из начала пути
                if (relativePath.StartsWith("/uploads/"))
                    relativePath = relativePath.Substring("/uploads/".Length);

                var filePath = Path.Combine(_environment.WebRootPath ?? _environment.ContentRootPath, "uploads", relativePath);

                if (File.Exists(filePath))
                {
                    await Task.Run(() => File.Delete(filePath));
                    _logger.LogInformation($"Image deleted: {filePath}");
                    return true;
                }

                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error deleting image: {ex.Message}");
                return false;
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
    }
}
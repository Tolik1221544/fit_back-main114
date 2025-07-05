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

                // ��������� ��� �����
                var allowedTypes = new[] { "image/jpeg", "image/jpg", "image/png", "image/webp" };
                if (!allowedTypes.Contains(image.ContentType.ToLowerInvariant()))
                    throw new ArgumentException("Invalid image type. Only JPEG, PNG and WebP are allowed.");

                // ������� ���������� ��� �����
                var extension = Path.GetExtension(image.FileName).ToLowerInvariant();
                if (string.IsNullOrEmpty(extension))
                    extension = ".jpg";

                var fileName = $"{Guid.NewGuid()}{extension}";
                var uploadsFolder = Path.Combine(_environment.WebRootPath ?? _environment.ContentRootPath, "uploads", folder);

                // ������� ����� ���� �� ����������
                Directory.CreateDirectory(uploadsFolder);

                var filePath = Path.Combine(uploadsFolder, fileName);

                // ��������� ����
                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await image.CopyToAsync(stream);
                }

                _logger.LogInformation($"Image saved: {fileName} in folder {folder}");

                // ���������� URL
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

                // ������� ���������� ��� �����
                var extension = Path.GetExtension(fileName).ToLowerInvariant();
                if (string.IsNullOrEmpty(extension))
                    extension = ".jpg";

                var uniqueFileName = $"{Guid.NewGuid()}{extension}";
                var uploadsFolder = Path.Combine(_environment.WebRootPath ?? _environment.ContentRootPath, "uploads", folder);

                // ������� ����� ���� �� ����������
                Directory.CreateDirectory(uploadsFolder);

                var filePath = Path.Combine(uploadsFolder, uniqueFileName);

                // ��������� ����
                await File.WriteAllBytesAsync(filePath, imageData);

                _logger.LogInformation($"Image saved from bytes: {uniqueFileName} in folder {folder}");

                // ���������� URL
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

                // ��������� ���� � ����� �� URL
                var uri = new Uri(imageUrl, UriKind.RelativeOrAbsolute);
                var relativePath = uri.IsAbsoluteUri ? uri.LocalPath : imageUrl;

                // ������� /uploads/ �� ������ ����
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
            // �������� ������� URL �� ������������ ��� �������� �������
            var baseUrl = GetBaseUrl();
            return $"{baseUrl}/uploads/{folder}/{fileName}";
        }

        private string GetBaseUrl()
        {
            // ������� �������� �������� �� ������������
            var configBaseUrl = _configuration["BaseUrl"];
            if (!string.IsNullOrEmpty(configBaseUrl))
                return configBaseUrl;

            // ���� ��� � ������������, ������ �� �������� �������
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
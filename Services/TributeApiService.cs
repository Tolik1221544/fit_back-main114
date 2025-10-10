using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace FitnessTracker.API.Services
{
    /// <summary>
    /// 💳 Сервис для работы с Tribute API
    /// </summary>
    public interface ITributeApiService
    {
        Task<TributeOrderStatus> GetOrderStatusAsync(string orderId);
        Task<bool> VerifyWebhookSignature(string payload, string signature);
    }

    public class TributeApiService : ITributeApiService
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;
        private readonly ILogger<TributeApiService> _logger;

        private string ApiKey => _configuration["Tribute:ApiKey"] ?? "";
        private string WebhookSecret => _configuration["Tribute:WebhookSecret"] ?? "";
        private string BaseUrl => "https://api.tribute.tg"; // Замените на реальный URL

        public TributeApiService(
            HttpClient httpClient,
            IConfiguration configuration,
            ILogger<TributeApiService> logger)
        {
            _httpClient = httpClient;
            _configuration = configuration;
            _logger = logger;

            _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {ApiKey}");
        }

        /// <summary>
        /// 🔍 Получить статус заказа из Tribute API
        /// </summary>
        public async Task<TributeOrderStatus> GetOrderStatusAsync(string orderId)
        {
            try
            {
                _logger.LogInformation($"🔍 Checking Tribute order status: {orderId}");

                var response = await _httpClient.GetAsync($"{BaseUrl}/v1/orders/{orderId}");

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning($"⚠️ Tribute API returned {response.StatusCode} for order {orderId}");
                    return new TributeOrderStatus
                    {
                        OrderId = orderId,
                        Status = "unknown",
                        ErrorMessage = $"API returned {response.StatusCode}"
                    };
                }

                var content = await response.Content.ReadAsStringAsync();
                var order = JsonSerializer.Deserialize<TributeOrderResponse>(content, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (order == null)
                {
                    _logger.LogError($"❌ Failed to parse Tribute response for order {orderId}");
                    return new TributeOrderStatus
                    {
                        OrderId = orderId,
                        Status = "error",
                        ErrorMessage = "Failed to parse API response"
                    };
                }

                _logger.LogInformation($"✅ Order {orderId} status: {order.Status}");

                return new TributeOrderStatus
                {
                    OrderId = order.OrderId ?? orderId,
                    Status = order.Status?.ToLower() ?? "unknown",
                    Amount = order.Amount,
                    Currency = order.Currency ?? "EUR",
                    TelegramId = order.Metadata?.TelegramId,
                    PackageId = order.Metadata?.PackageId,
                    CreatedAt = order.CreatedAt,
                    CompletedAt = order.CompletedAt
                };
            }
            catch (Exception ex)
            {
                _logger.LogError($"❌ Error checking Tribute order {orderId}: {ex.Message}");
                return new TributeOrderStatus
                {
                    OrderId = orderId,
                    Status = "error",
                    ErrorMessage = ex.Message
                };
            }
        }

        /// <summary>
        /// 🔐 Проверить подпись webhook от Tribute
        /// </summary>
        public Task<bool> VerifyWebhookSignature(string payload, string signature)
        {
            try
            {
                using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(WebhookSecret));
                var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
                var computedSignature = Convert.ToHexString(hash).ToLower();

                var isValid = computedSignature == signature.ToLower();

                if (!isValid)
                {
                    _logger.LogWarning($"⚠️ Invalid webhook signature");
                }

                return Task.FromResult(isValid);
            }
            catch (Exception ex)
            {
                _logger.LogError($"❌ Error verifying signature: {ex.Message}");
                return Task.FromResult(false);
            }
        }
    }

    /// <summary>
    /// 📊 Статус заказа из Tribute
    /// </summary>
    public class TributeOrderStatus
    {
        public string OrderId { get; set; } = "";
        public string Status { get; set; } = ""; // pending, completed, failed, cancelled
        public decimal Amount { get; set; }
        public string Currency { get; set; } = "EUR";
        public string? TelegramId { get; set; }
        public string? PackageId { get; set; }
        public DateTime? CreatedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public string? ErrorMessage { get; set; }
    }

    /// <summary>
    /// 📦 Ответ от Tribute API
    /// </summary>
    public class TributeOrderResponse
    {
        public string? OrderId { get; set; }
        public string? Status { get; set; }
        public decimal Amount { get; set; }
        public string? Currency { get; set; }
        public TributeOrderMetadata? Metadata { get; set; }
        public DateTime? CreatedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
    }

    public class TributeOrderMetadata
    {
        public string? TelegramId { get; set; }
        public string? PackageId { get; set; }
    }
}
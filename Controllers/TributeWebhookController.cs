using Microsoft.AspNetCore.Mvc;
using System.Security.Cryptography;
using System.Text;
using FitnessTracker.API.Services;
using FitnessTracker.API.Repositories;
using FitnessTracker.API.Models;
using Microsoft.EntityFrameworkCore;

namespace FitnessTracker.API.Controllers
{
	[ApiController]
	[Route("api/tribute")]
	public class TributeWebhookController : ControllerBase
	{
		private readonly ILwCoinService _lwCoinService;
		private readonly IUserRepository _userRepository;
		private readonly IConfiguration _configuration;
		private readonly ILogger<TributeWebhookController> _logger;

		public TributeWebhookController(
			ILwCoinService lwCoinService,
			IUserRepository userRepository,
			IConfiguration configuration,
			ILogger<TributeWebhookController> logger)
		{
			_lwCoinService = lwCoinService;
			_userRepository = userRepository;
			_configuration = configuration;
			_logger = logger;
		}

		[HttpPost("webhook")]
		public async Task<IActionResult> HandleWebhook()
		{
			try
			{
				// Читаем тело запроса
				using var reader = new StreamReader(Request.Body);
				var body = await reader.ReadToEndAsync();

				// Получаем подпись из заголовка
				var signature = Request.Headers["X-Tribute-Signature"].FirstOrDefault();

				if (string.IsNullOrEmpty(signature))
				{
					_logger.LogWarning("⚠️ Webhook без подписи");
					return BadRequest(new { error = "Missing signature" });
				}

				// Проверяем подпись
				var webhookSecret = _configuration["Tribute:WebhookSecret"];
				if (!VerifySignature(body, signature, webhookSecret))
				{
					_logger.LogWarning("❌ Неверная подпись webhook");
					return Unauthorized(new { error = "Invalid signature" });
				}

				// Парсим данные
				var data = System.Text.Json.JsonSerializer.Deserialize<TributeWebhookData>(body);

				if (data == null)
				{
					return BadRequest(new { error = "Invalid payload" });
				}

				_logger.LogInformation($"💳 Tribute webhook: {data.Status} для заказа {data.OrderId}");

				// Обрабатываем только успешные платежи
				if (data.Status == "success" || data.Status == "completed")
				{
					await ProcessSuccessfulPayment(data);
				}

				return Ok(new { status = "ok" });
			}
			catch (Exception ex)
			{
				_logger.LogError($"❌ Ошибка обработки webhook: {ex.Message}");
				return StatusCode(500, new { error = "Internal error" });
			}
		}

		private bool VerifySignature(string body, string signature, string secret)
		{
			using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
			var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(body));
			var computedSignature = Convert.ToHexString(hash).ToLower();

			return computedSignature == signature.ToLower();
		}

        private async Task ProcessSuccessfulPayment(TributeWebhookData data)
        {
            try
            {
                var telegramIdStr = data.Metadata?.TelegramId;

                if (string.IsNullOrEmpty(telegramIdStr))
                {
                    _logger.LogWarning("⚠️ Нет telegram_id в metadata");
                    return;
                }

                if (!long.TryParse(telegramIdStr, out var telegramId))
                {
                    _logger.LogWarning($"⚠️ Неверный формат telegram_id: {telegramIdStr}");
                    return;
                }

                var user = await _userRepository.GetByTelegramIdAsync(telegramId);

                if (user == null)
                {
                    _logger.LogWarning($"⚠️ Пользователь с Telegram ID {telegramId} не найден");
                    return;
                }

                var coins = DetermineCoinsFromAmount(data.Amount);
                var days = DetermineDaysFromAmount(data.Amount);

                await _lwCoinService.PurchaseSubscriptionCoinsAsync(
                    user.Id,
                    coins,
                    days,
                    data.Amount
                );

                _logger.LogInformation($"✅ Начислено {coins} монет пользователю {user.Email} (TG: {telegramId}) на {days} дней");
            }
            catch (Exception ex)
            {
                _logger.LogError($"❌ Ошибка начисления монет: {ex.Message}");
            }
        }

        private int DetermineCoinsFromAmount(decimal amount)
		{
			return amount switch
			{
				0.99m => 50,
				1.99m => 100,
				2.99m => 100,
				3.99m => 200,
				7.99m => 500,
				8.99m => 9999, // Premium unlimited
				_ => 0
			};
		}

		private int DetermineDaysFromAmount(decimal amount)
		{
			return amount switch
			{
				0.99m => 7,
				1.99m => 14,
				2.99m => 30,
				3.99m => 30,
				7.99m => 30,
				8.99m => 30,
				_ => 0
			};
		}
	}

	public class TributeWebhookData
	{
		[System.Text.Json.Serialization.JsonPropertyName("order_id")]
		public string OrderId { get; set; } = "";

		[System.Text.Json.Serialization.JsonPropertyName("status")]
		public string Status { get; set; } = "";

		[System.Text.Json.Serialization.JsonPropertyName("amount")]
		public decimal Amount { get; set; }

		[System.Text.Json.Serialization.JsonPropertyName("currency")]
		public string Currency { get; set; } = "RUB";

		[System.Text.Json.Serialization.JsonPropertyName("metadata")]
		public TributeMetadata? Metadata { get; set; }
	}

	public class TributeMetadata
	{
		[System.Text.Json.Serialization.JsonPropertyName("telegram_id")]
		public string? TelegramId { get; set; }
	}
}
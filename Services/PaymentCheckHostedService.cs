using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using FitnessTracker.API.Data;
using FitnessTracker.API.Repositories;
using Microsoft.EntityFrameworkCore;

namespace FitnessTracker.API.Services
{
    /// <summary>
    /// 🔄 Фоновый сервис для проверки pending платежей
    /// </summary>
    public class PaymentCheckHostedService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<PaymentCheckHostedService> _logger;
        private readonly TimeSpan _checkInterval;

        public PaymentCheckHostedService(
            IServiceProvider serviceProvider,
            ILogger<PaymentCheckHostedService> logger,
            IConfiguration configuration)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;

            // Проверяем каждые 2 минуты
            var intervalMinutes = configuration.GetValue<int>("PaymentCheck:IntervalMinutes", 2);
            _checkInterval = TimeSpan.FromMinutes(intervalMinutes);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("💳 Payment Check Service started");

            // Ждём 30 секунд перед первым запуском
            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await CheckPendingPaymentsAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogError($"❌ Error in Payment Check Service: {ex.Message}");
                }

                await Task.Delay(_checkInterval, stoppingToken);
            }

            _logger.LogInformation("💳 Payment Check Service stopped");
        }

        /// <summary>
        /// 🔍 Проверить все pending платежи
        /// </summary>
        private async Task CheckPendingPaymentsAsync()
        {
            using var scope = _serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var tributeService = scope.ServiceProvider.GetRequiredService<ITributeApiService>();
            var lwCoinService = scope.ServiceProvider.GetRequiredService<ILwCoinService>();
            var userRepository = scope.ServiceProvider.GetRequiredService<IUserRepository>();

            try
            {
                // Ищем платежи со статусом pending старше 2 минут
                var cutoffTime = DateTime.UtcNow.AddMinutes(-2);

                // ВАЖНО: Нужна таблица Payment в ApplicationDbContext
                // Если её нет - добавьте в database.py бота или создайте в бэке

                var pendingPayments = await context.Set<PendingPayment>()
                    .Where(p => p.Status == "pending" && p.CreatedAt < cutoffTime)
                    .Take(10) // Проверяем максимум 10 за раз
                    .ToListAsync();

                if (!pendingPayments.Any())
                {
                    _logger.LogDebug("✅ No pending payments to check");
                    return;
                }

                _logger.LogInformation($"🔍 Checking {pendingPayments.Count} pending payments");

                foreach (var payment in pendingPayments)
                {
                    try
                    {
                        var status = await tributeService.GetOrderStatusAsync(payment.PaymentId);

                        if (status.Status == "completed" || status.Status == "success")
                        {
                            _logger.LogInformation($"✅ Payment {payment.PaymentId} completed, processing...");

                            // Начисляем монеты
                            var user = await userRepository.GetByTelegramIdAsync(payment.TelegramId);
                            if (user != null)
                            {
                                var (coins, days) = DeterminePackageFromAmount(status.Amount);

                                await lwCoinService.PurchaseSubscriptionCoinsAsync(
                                    user.Id,
                                    coins,
                                    days,
                                    status.Amount
                                );

                                // Обновляем статус
                                payment.Status = "completed";
                                payment.CompletedAt = DateTime.UtcNow;
                                await context.SaveChangesAsync();

                                _logger.LogInformation($"✅ Payment processed: {coins} coins for user {user.Email}");
                            }
                        }
                        else if (status.Status == "failed" || status.Status == "cancelled")
                        {
                            _logger.LogWarning($"⚠️ Payment {payment.PaymentId} {status.Status}");

                            payment.Status = status.Status;
                            payment.CompletedAt = DateTime.UtcNow;
                            await context.SaveChangesAsync();
                        }
                        else if (status.Status == "pending")
                        {
                            // Если прошло больше 10 минут - помечаем как expired
                            if (DateTime.UtcNow - payment.CreatedAt > TimeSpan.FromMinutes(10))
                            {
                                _logger.LogWarning($"⏰ Payment {payment.PaymentId} expired (>10 min)");
                                payment.Status = "expired";
                                await context.SaveChangesAsync();
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError($"❌ Error checking payment {payment.PaymentId}: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"❌ Error in CheckPendingPaymentsAsync: {ex.Message}");
            }
        }

        private (int coins, int days) DeterminePackageFromAmount(decimal amount)
        {
            return amount switch
            {
                2m => (100, 30),
                5m => (300, 90),
                10m => (600, 180),
                20m => (1200, 365),
                _ => (0, 0)
            };
        }
    }

    /// <summary>
    /// 📦 Модель pending платежа (добавить в ApplicationDbContext)
    /// </summary>
    public class PendingPayment
    {
        public int Id { get; set; }
        public string PaymentId { get; set; } = ""; // order_id от Tribute
        public long TelegramId { get; set; }
        public decimal Amount { get; set; }
        public string Currency { get; set; } = "EUR";
        public string Status { get; set; } = "pending"; // pending, completed, failed, cancelled, expired
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? CompletedAt { get; set; }
    }
}
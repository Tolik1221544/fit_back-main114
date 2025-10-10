using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using FitnessTracker.API.Data;
using FitnessTracker.API.Repositories;
using FitnessTracker.API.Models; 
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

            var intervalMinutes = configuration.GetValue<int>("PaymentCheck:IntervalMinutes", 2);
            _checkInterval = TimeSpan.FromMinutes(intervalMinutes);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("💳 Payment Check Service started");

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

        private async Task CheckPendingPaymentsAsync()
        {
            using var scope = _serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var tributeService = scope.ServiceProvider.GetRequiredService<ITributeApiService>();
            var lwCoinService = scope.ServiceProvider.GetRequiredService<ILwCoinService>();
            var userRepository = scope.ServiceProvider.GetRequiredService<IUserRepository>();

            try
            {
                var cutoffTime = DateTime.UtcNow.AddMinutes(-2);

                var pendingPayments = await context.PendingPayments
                    .Where(p => p.Status == "pending" && p.CreatedAt < cutoffTime)
                    .Take(10)
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

                            var user = await userRepository.GetByTelegramIdAsync(payment.TelegramId);
                            if (user != null)
                            {
                                await lwCoinService.PurchaseSubscriptionCoinsAsync(
                                    user.Id,
                                    payment.CoinsAmount,
                                    payment.DurationDays,
                                    payment.Amount
                                );

                                payment.Status = "completed";
                                payment.CompletedAt = DateTime.UtcNow;
                                await context.SaveChangesAsync();

                                _logger.LogInformation($"✅ Payment processed: {payment.CoinsAmount} coins for user {user.Email}");
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
    }
}
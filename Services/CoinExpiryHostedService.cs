using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using FitnessTracker.API.Data;
using Microsoft.EntityFrameworkCore;

namespace FitnessTracker.API.Services
{
    /// <summary>
    /// ✅ Фоновый сервис для автоматического списания истекших подписочных монет
    /// </summary>
    public class CoinExpiryHostedService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<CoinExpiryHostedService> _logger;
        private readonly TimeSpan _checkInterval;

        public CoinExpiryHostedService(
            IServiceProvider serviceProvider,
            ILogger<CoinExpiryHostedService> logger,
            IConfiguration configuration)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;

            var intervalMinutes = configuration.GetValue<int>("CoinExpiry:CheckIntervalMinutes", 30);
            _checkInterval = TimeSpan.FromMinutes(intervalMinutes);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("💰 Coin Expiry Service started");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await ProcessExpiredCoinsAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogError($"❌ Error in Coin Expiry Service: {ex.Message}");
                }

                await Task.Delay(_checkInterval, stoppingToken);
            }

            _logger.LogInformation("💰 Coin Expiry Service stopped");
        }

        private async Task ProcessExpiredCoinsAsync()
        {
            using var scope = _serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var userRepository = scope.ServiceProvider.GetRequiredService<IUserRepository>();

            var now = DateTime.UtcNow;

            var expiredTransactions = await context.LwCoinTransactions
                .Include(t => t.User)
                .Where(t =>
                    t.CoinSource == "subscription" &&
                    t.ExpiryDate.HasValue &&
                    t.ExpiryDate <= now &&
                    !t.IsExpired &&
                    t.Amount > 0) 
                .ToListAsync();

            if (!expiredTransactions.Any())
            {
                _logger.LogDebug("✅ No expired subscription coins found");
                return;
            }

            _logger.LogInformation($"🔄 Processing {expiredTransactions.Count} expired coin transactions");

            var userGroups = expiredTransactions.GroupBy(t => t.UserId);

            foreach (var userGroup in userGroups)
            {
                var userId = userGroup.Key;
                var user = await userRepository.GetByIdAsync(userId);

                if (user == null)
                {
                    _logger.LogWarning($"⚠️ User {userId} not found for coin expiry");
                    continue;
                }

                decimal totalExpiredCoins = 0;

                foreach (var transaction in userGroup)
                {
                    var expiredAmount = transaction.FractionalAmount > 0 ?
                        (decimal)transaction.FractionalAmount : transaction.Amount;

                    totalExpiredCoins += expiredAmount;

                    transaction.IsExpired = true;

                    _logger.LogInformation($"💸 Expired {expiredAmount} subscription coins for user {userId} " +
                        $"(Transaction: {transaction.Id}, Expired: {transaction.ExpiryDate})");
                }

                if (totalExpiredCoins > 0)
                {
                    var newBalance = Math.Max(0, user.FractionalLwCoins - (double)totalExpiredCoins);
                    user.FractionalLwCoins = newBalance;
                    user.LwCoins = (int)Math.Floor(newBalance);

                    await userRepository.UpdateAsync(user);

                    var expiryTransaction = new LwCoinTransaction
                    {
                        UserId = userId,
                        Amount = -(int)Math.Ceiling(totalExpiredCoins),
                        FractionalAmount = -(double)totalExpiredCoins,
                        Type = "expired",
                        Description = $"Subscription coins expired ({totalExpiredCoins} coins)",
                        CoinSource = "subscription",
                        FeatureUsed = "auto_expiry",
                        IsExpired = false 
                    };

                    context.LwCoinTransactions.Add(expiryTransaction);

                    _logger.LogInformation($"✅ Expired {totalExpiredCoins} subscription coins for user {userId}. " +
                        $"New balance: {newBalance}");

                    await NotifyUserAboutExpiredCoinsAsync(userId, totalExpiredCoins);
                }
            }

            await context.SaveChangesAsync();

            _logger.LogInformation($"✅ Processed {expiredTransactions.Count} expired transactions");
        }

        private async Task NotifyUserAboutExpiredCoinsAsync(string userId, decimal expiredAmount)
        {
            _logger.LogInformation($"📧 Notification queued for user {userId}: {expiredAmount} coins expired");
            await Task.CompletedTask;
        }
    }
}
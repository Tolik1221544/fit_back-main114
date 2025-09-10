using FitnessTracker.API.DTOs;

namespace FitnessTracker.API.Services
{
    public interface ILwCoinService
    {
        Task<LwCoinBalanceDto> GetUserLwCoinBalanceAsync(string userId);
        Task<bool> SpendLwCoinsAsync(string userId, int amount, string type, string description, string featureUsed = "");
        Task<bool> AddLwCoinsAsync(string userId, int amount, string type, string description);
        Task<IEnumerable<LwCoinTransactionDto>> GetUserLwCoinTransactionsAsync(string userId);
        Task<bool> ProcessMonthlyRefillAsync(string userId);
        Task<bool> PurchasePremiumAsync(string userId, PurchasePremiumRequest request);
        Task<bool> PurchaseCoinPackAsync(string userId, PurchaseCoinPackRequest request);
        Task<LwCoinLimitsDto> GetUserLimitsAsync(string userId);
        Task<SubscriptionStatusDto> GetSubscriptionStatusAsync(string userId);

        Task<bool> SetUserCoinsAsync(string userId, decimal amount, string source = "manual");
        Task<bool> AddRegistrationBonusAsync(string userId);
        Task<bool> PurchaseSubscriptionCoinsAsync(string userId, int coinsAmount, int durationDays, decimal price);
    }
}
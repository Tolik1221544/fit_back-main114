using FitnessTracker.API.DTOs;

namespace FitnessTracker.API.Services
{
    public interface ICoinService
    {
        Task<CoinBalanceDto> GetUserCoinBalanceAsync(string userId);
        Task<IEnumerable<CoinTransactionDto>> GetUserTransactionsAsync(string userId);
        Task<CoinBalanceDto> PurchaseCoinsAsync(string userId, PurchaseCoinRequest request);
        Task<bool> SpendCoinsAsync(string userId, int amount, string description);
        Task<bool> EarnCoinsAsync(string userId, int amount, string description);
    }
}

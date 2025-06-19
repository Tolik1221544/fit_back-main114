using FitnessTracker.API.Models;

namespace FitnessTracker.API.Repositories
{
    public interface ICoinRepository
    {
        Task<IEnumerable<CoinTransaction>> GetUserTransactionsAsync(string userId);
        Task<CoinTransaction> CreateTransactionAsync(CoinTransaction transaction);
        Task<int> GetUserCoinBalanceAsync(string userId);
    }
}

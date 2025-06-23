using FitnessTracker.API.Models;

namespace FitnessTracker.API.Repositories
{
    public interface ILwCoinRepository
    {
        Task<IEnumerable<LwCoinTransaction>> GetUserTransactionsAsync(string userId);
        Task<LwCoinTransaction> CreateTransactionAsync(LwCoinTransaction transaction);
        Task<Subscription> CreateSubscriptionAsync(Subscription subscription);
        Task<IEnumerable<Subscription>> GetUserSubscriptionsAsync(string userId);
    }
}
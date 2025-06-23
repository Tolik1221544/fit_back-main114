using FitnessTracker.API.Data;
using FitnessTracker.API.Models;
using Microsoft.EntityFrameworkCore;

namespace FitnessTracker.API.Repositories
{
    public class LwCoinRepository : ILwCoinRepository
    {
        private readonly ApplicationDbContext _context;

        public LwCoinRepository(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IEnumerable<LwCoinTransaction>> GetUserTransactionsAsync(string userId)
        {
            return await _context.LwCoinTransactions
                .Where(t => t.UserId == userId)
                .OrderByDescending(t => t.CreatedAt)
                .ToListAsync();
        }

        public async Task<LwCoinTransaction> CreateTransactionAsync(LwCoinTransaction transaction)
        {
            _context.LwCoinTransactions.Add(transaction);
            await _context.SaveChangesAsync();
            return transaction;
        }

        public async Task<Subscription> CreateSubscriptionAsync(Subscription subscription)
        {
            _context.Subscriptions.Add(subscription);
            await _context.SaveChangesAsync();
            return subscription;
        }

        public async Task<IEnumerable<Subscription>> GetUserSubscriptionsAsync(string userId)
        {
            return await _context.Subscriptions
                .Where(s => s.UserId == userId)
                .OrderByDescending(s => s.PurchasedAt)
                .ToListAsync();
        }
    }
}
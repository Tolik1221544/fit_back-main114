using FitnessTracker.API.Models;

namespace FitnessTracker.API.Repositories
{
    public interface IUserRepository
    {
        Task<User?> GetByIdAsync(string id);
        Task<User?> GetByEmailAsync(string email);
        Task<User?> GetByTelegramIdAsync(long telegramId);
        Task<User> CreateAsync(User user);
        Task<User> UpdateAsync(User user);
        Task DeleteAsync(string id);
        Task<bool> ExistsAsync(string id);
        Task<IEnumerable<User>> GetAllUsersAsync();
        Task<User?> GetByReferralCodeAsync(string referralCode);
    }
}
using FitnessTracker.API.Models;

namespace FitnessTracker.API.Repositories
{
    public interface IReferralRepository
    {
        Task<Referral?> GetByCodeAsync(string code);
        Task<Referral> CreateAsync(Referral referral);
        Task<IEnumerable<Referral>> GetUserReferralsAsync(string userId);
        Task<bool> CodeExistsAsync(string code);
    }
}

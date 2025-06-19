using FitnessTracker.API.Data;
using FitnessTracker.API.Models;
using Microsoft.EntityFrameworkCore;

namespace FitnessTracker.API.Repositories
{
    public class ReferralRepository : IReferralRepository
    {
        private readonly ApplicationDbContext _context;

        public ReferralRepository(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<Referral?> GetByCodeAsync(string code)
        {
            return await _context.Referrals.FirstOrDefaultAsync(r => r.ReferralCode == code);
        }

        public async Task<Referral> CreateAsync(Referral referral)
        {
            _context.Referrals.Add(referral);
            await _context.SaveChangesAsync();
            return referral;
        }

        public async Task<IEnumerable<Referral>> GetUserReferralsAsync(string userId)
        {
            return await _context.Referrals
                .Where(r => r.ReferrerId == userId)
                .OrderByDescending(r => r.CreatedAt)
                .ToListAsync();
        }

        public async Task<bool> CodeExistsAsync(string code)
        {
            return await _context.Referrals.AnyAsync(r => r.ReferralCode == code);
        }
    }
}

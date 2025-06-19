using FitnessTracker.API.Data;
using FitnessTracker.API.Models;
using Microsoft.EntityFrameworkCore;

namespace FitnessTracker.API.Repositories
{
    public class SkinRepository : ISkinRepository
    {
        private readonly ApplicationDbContext _context;

        public SkinRepository(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IEnumerable<Skin>> GetAllSkinsAsync()
        {
            return await _context.Skins.ToListAsync();
        }

        public async Task<Skin?> GetSkinByIdAsync(string id)
        {
            return await _context.Skins.FirstOrDefaultAsync(s => s.Id == id);
        }

        public async Task<IEnumerable<UserSkin>> GetUserSkinsAsync(string userId)
        {
            return await _context.UserSkins
                .Include(us => us.Skin)
                .Where(us => us.UserId == userId)
                .ToListAsync();
        }

        public async Task<UserSkin> PurchaseSkinAsync(UserSkin userSkin)
        {
            _context.UserSkins.Add(userSkin);
            await _context.SaveChangesAsync();
            return userSkin;
        }

        public async Task<bool> UserOwnsSkinAsync(string userId, string skinId)
        {
            return await _context.UserSkins
                .AnyAsync(us => us.UserId == userId && us.SkinId == skinId);
        }
    }
}

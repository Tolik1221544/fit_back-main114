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
            return await _context.Skins
                .OrderBy(s => s.Tier)        // Сначала по уровню (1, 2, 3)
                .ThenBy(s => s.Cost)         // Затем по цене внутри уровня
                .ToListAsync();
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
                .OrderBy(us => us.Skin.Tier)    
                .ThenBy(us => us.Skin.Cost)      // Затем по цене
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

        public async Task<bool> DeactivateAllUserSkinsAsync(string userId)
        {
            var userSkins = await _context.UserSkins
                .Where(us => us.UserId == userId && us.IsActive)
                .ToListAsync();

            foreach (var userSkin in userSkins)
            {
                userSkin.IsActive = false;
            }

            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> ActivateUserSkinAsync(string userId, string skinId)
        {
            var userSkin = await _context.UserSkins
                .FirstOrDefaultAsync(us => us.UserId == userId && us.SkinId == skinId);

            if (userSkin == null) return false;

            userSkin.IsActive = true;
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<UserSkin?> GetActiveUserSkinAsync(string userId)
        {
            return await _context.UserSkins
                .Include(us => us.Skin)
                .FirstOrDefaultAsync(us => us.UserId == userId && us.IsActive);
        }
    }
}
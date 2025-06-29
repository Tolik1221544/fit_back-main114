using FitnessTracker.API.Models;

namespace FitnessTracker.API.Repositories
{
    public interface ISkinRepository
    {
        Task<IEnumerable<Skin>> GetAllSkinsAsync();
        Task<Skin?> GetSkinByIdAsync(string id);
        Task<IEnumerable<UserSkin>> GetUserSkinsAsync(string userId);
        Task<UserSkin> PurchaseSkinAsync(UserSkin userSkin);
        Task<bool> UserOwnsSkinAsync(string userId, string skinId);

        Task<bool> DeactivateAllUserSkinsAsync(string userId);
        Task<bool> ActivateUserSkinAsync(string userId, string skinId);
        Task<UserSkin?> GetActiveUserSkinAsync(string userId);
    }
}
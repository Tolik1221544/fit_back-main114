using FitnessTracker.API.DTOs;

namespace FitnessTracker.API.Services
{
    public interface ISkinService
    {
        Task<IEnumerable<SkinDto>> GetAllSkinsAsync(string userId);
        Task<bool> PurchaseSkinAsync(string userId, PurchaseSkinRequest request);
        Task<IEnumerable<SkinDto>> GetUserSkinsAsync(string userId);

        Task<bool> ActivateSkinAsync(string userId, ActivateSkinRequest request);
        Task<SkinDto?> GetActiveUserSkinAsync(string userId);
        Task<decimal> GetUserExperienceBoostAsync(string userId);
    }
}
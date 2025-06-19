using FitnessTracker.API.DTOs;

namespace FitnessTracker.API.Services
{
    public interface ISkinService
    {
        Task<IEnumerable<SkinDto>> GetAllSkinsAsync(string userId);
        Task<bool> PurchaseSkinAsync(string userId, PurchaseSkinRequest request);
        Task<IEnumerable<SkinDto>> GetUserSkinsAsync(string userId);
    }
}

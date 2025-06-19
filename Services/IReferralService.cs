using FitnessTracker.API.DTOs;

namespace FitnessTracker.API.Services
{
    public interface IReferralService
    {
        Task<bool> SetReferralAsync(string userId, SetReferralRequest request);
        Task<string> GenerateReferralCodeAsync(string userId);
        Task<int> GetReferralCountAsync(string userId);
    }
}

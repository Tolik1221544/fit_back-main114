using FitnessTracker.API.DTOs;

namespace FitnessTracker.API.Services
{
    public interface IAuthService
    {
        Task<bool> SendVerificationCodeAsync(string email);
        Task<AuthResponse> ConfirmEmailAsync(string email, string code);
        Task<AuthResponse> GoogleAuthAsync(string googleToken);
        Task<bool> LogoutAsync(string accessToken);
        Task<string> GenerateJwtTokenAsync(string userId);
    }
}

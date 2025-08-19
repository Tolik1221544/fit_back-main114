using FitnessTracker.API.DTOs;

namespace FitnessTracker.API.Services
{
    public interface IGoogleAuthService
    {
        Task<AuthResponseDto> AuthenticateWithIdTokenAsync(string idToken);
        Task<AuthResponseDto> AuthenticateWithServerCodeAsync(string serverAuthCode);
        Task<AuthResponseDto> AuthenticateGoogleTokenAsync(string googleToken);
    }
}
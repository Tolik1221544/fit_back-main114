using FitnessTracker.API.DTOs;

namespace FitnessTracker.API.Services
{
    public interface IGoogleAuthService
    {
        Task<AuthResponseDto> AuthenticateGoogleTokenAsync(string googleToken);
    }
}
using FitnessTracker.API.DTOs;

namespace FitnessTracker.API.Services
{
    public interface IUserService
    {
        Task<UserDto?> GetUserByIdAsync(string userId);
        Task<UserDto> UpdateUserProfileAsync(string userId, UpdateUserProfileRequest request);
        Task DeleteUserAsync(string userId);
        Task DeleteUserCompletelyAsync(string userId); 
    }
}

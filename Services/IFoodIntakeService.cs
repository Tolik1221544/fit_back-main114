using FitnessTracker.API.DTOs;

namespace FitnessTracker.API.Services
{
    public interface IFoodIntakeService
    {
        Task<IEnumerable<FoodIntakeDto>> GetUserFoodIntakesAsync(string userId);
        Task<IEnumerable<FoodIntakeDto>> GetUserFoodIntakesByDateAsync(string userId, DateTime date);
        Task<IEnumerable<FoodIntakeDto>> AddFoodIntakeAsync(string userId, AddFoodIntakeRequest request);
        Task<FoodIntakeDto> UpdateFoodIntakeAsync(string userId, string foodIntakeId, UpdateFoodIntakeRequest request);
        Task<FoodIntakeDto?> GetFoodItemByIdAsync(string userId, string foodIntakeId);
        Task DeleteFoodIntakeAsync(string userId, string foodIntakeId);
        Task<ScanFoodResponse> ScanFoodAsync(string userId, byte[] imageData);
    }
}

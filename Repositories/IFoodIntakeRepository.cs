using FitnessTracker.API.Models;

namespace FitnessTracker.API.Repositories
{
    public interface IFoodIntakeRepository
    {
        Task<IEnumerable<FoodIntake>> GetByUserIdAsync(string userId);
        Task<IEnumerable<FoodIntake>> GetByUserIdAndDateAsync(string userId, DateTime date);
        Task<FoodIntake?> GetByIdAsync(string id);
        Task<FoodIntake> CreateAsync(FoodIntake foodIntake);
        Task<FoodIntake> UpdateAsync(FoodIntake foodIntake);
        Task DeleteAsync(string id);
        Task<IEnumerable<FoodIntake>> CreateManyAsync(IEnumerable<FoodIntake> foodIntakes);
    }
}

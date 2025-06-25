using FitnessTracker.API.DTOs;

namespace FitnessTracker.API.Services
{
    public interface IExperienceService
    {
        Task<bool> AddExperienceAsync(string userId, int experience, string source, string description);
        Task<IEnumerable<ExperienceTransactionDto>> GetUserExperienceTransactionsAsync(string userId);
        Task<int> CalculateLevelFromExperience(int experience);
        Task<int> GetExperienceForNextLevel(int currentLevel);
    }
}
using FitnessTracker.API.DTOs;
using FitnessTracker.API.Models;
using FitnessTracker.API.Repositories;
using AutoMapper;

namespace FitnessTracker.API.Services
{
    public class ExperienceService : IExperienceService
    {
        private readonly IExperienceRepository _experienceRepository;
        private readonly IUserRepository _userRepository;
        private readonly IMapper _mapper;
        private readonly ILogger<ExperienceService> _logger;

        private static readonly int[] LevelExperienceRequirements = {
            0, 100, 250, 450, 700, 1000, 1350, 1750, 2200, 2700, 3250,
            3850, 4500, 5200, 5950, 6750, 7600, 8500, 9450, 10450, 11500
        };

        public ExperienceService(
            IExperienceRepository experienceRepository,
            IUserRepository userRepository,
            IMapper mapper,
            ILogger<ExperienceService> logger)
        {
            _experienceRepository = experienceRepository;
            _userRepository = userRepository;
            _mapper = mapper;
            _logger = logger;
        }

        public async Task<bool> AddExperienceAsync(string userId, int experience, string source, string description)
        {
            var user = await _userRepository.GetByIdAsync(userId);
            if (user == null) return false;

            var levelBefore = user.Level;
            user.Experience += experience;
            var levelAfter = await CalculateLevelFromExperience(user.Experience);
            var leveledUp = levelAfter > levelBefore;

            if (leveledUp)
            {
                user.Level = levelAfter;
                _logger.LogInformation($"User {userId} leveled up from {levelBefore} to {levelAfter}!");
            }

            await _userRepository.UpdateAsync(user);

            var transaction = new ExperienceTransaction
            {
                UserId = userId,
                Experience = experience,
                Source = source,
                Description = description,
                LevelBefore = levelBefore,
                LevelAfter = levelAfter,
                LeveledUp = leveledUp
            };

            await _experienceRepository.CreateTransactionAsync(transaction);

            _logger.LogInformation($"Added {experience} XP to user {userId} from {source}");
            return true;
        }

        public async Task<IEnumerable<ExperienceTransactionDto>> GetUserExperienceTransactionsAsync(string userId)
        {
            var transactions = await _experienceRepository.GetUserTransactionsAsync(userId);
            return _mapper.Map<IEnumerable<ExperienceTransactionDto>>(transactions);
        }

        public async Task<int> CalculateLevelFromExperience(int experience)
        {
            for (int level = LevelExperienceRequirements.Length - 1; level >= 1; level--)
            {
                if (experience >= LevelExperienceRequirements[level])
                {
                    return level;
                }
            }
            return 1;
        }

        public async Task<int> GetExperienceForNextLevel(int currentLevel)
        {
            if (currentLevel >= LevelExperienceRequirements.Length - 1)
            {
                return LevelExperienceRequirements[^1]; // Max level
            }
            return LevelExperienceRequirements[currentLevel + 1];
        }
    }
}
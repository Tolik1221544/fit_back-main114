using FitnessTracker.API.DTOs;
using FitnessTracker.API.Models;
using FitnessTracker.API.Repositories;
using AutoMapper;

namespace FitnessTracker.API.Services
{
    public class ExperienceService : IExperienceService
    {
        private readonly IUserRepository _userRepository;
        private readonly IExperienceRepository _experienceRepository;
        private readonly IMapper _mapper;
        private readonly ILogger<ExperienceService> _logger;

        public ExperienceService(
            IUserRepository userRepository,
            IExperienceRepository experienceRepository,
            IMapper mapper,
            ILogger<ExperienceService> logger)
        {
            _userRepository = userRepository;
            _experienceRepository = experienceRepository;
            _mapper = mapper;
            _logger = logger;
        }

        public async Task<bool> AddExperienceAsync(string userId, int experience, string source, string description)
        {
            var user = await _userRepository.GetByIdAsync(userId);
            if (user == null) return false;

            var levelBefore = user.Level;
            user.Experience += experience;

            // Вычисляем новый уровень
            var levelAfter = await CalculateLevelFromExperience(user.Experience);
            var leveledUp = levelAfter > levelBefore;

            if (leveledUp)
            {
                user.Level = levelAfter;
                _logger.LogInformation($"User {userId} leveled up from {levelBefore} to {levelAfter}!");
            }

            await _userRepository.UpdateAsync(user);

            // Создаем запись транзакции опыта
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

            await _experienceRepository.CreateAsync(transaction);

            _logger.LogInformation($"Added {experience} XP to user {userId}: {description}");
            return true;
        }

        public async Task<IEnumerable<ExperienceTransactionDto>> GetUserExperienceTransactionsAsync(string userId)
        {
            var transactions = await _experienceRepository.GetByUserIdAsync(userId);
            return _mapper.Map<IEnumerable<ExperienceTransactionDto>>(transactions);
        }

        public async Task<int> CalculateLevelFromExperience(int experience)
        {
            // floor(sqrt(experience / 100)) + 1
            if (experience < 100) return 1;

            return (int)Math.Floor(Math.Sqrt(experience / 100.0)) + 1;
        }

        public async Task<int> GetExperienceForNextLevel(int currentLevel)
        {
            return currentLevel * currentLevel * 100;
        }
    }
}
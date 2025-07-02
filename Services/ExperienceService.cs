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
        private readonly ISkinService _skinService;
        private readonly IMapper _mapper;
        private readonly ILogger<ExperienceService> _logger;

  
        private static readonly int[] LevelExperienceRequirements = {
            0,     // Уровень 0 (не используется)
            100,   // Уровень 1 -> 2 (100 опыта)
            250,   // Уровень 2 -> 3 (250 опыта)
            450,   // Уровень 3 -> 4 (450 опыта)
            700,   // Уровень 4 -> 5 (700 опыта)
            1000,  // Уровень 5 -> 6 (1000 опыта)
            1350,  // Уровень 6 -> 7 (1350 опыта)
            1750,  // Уровень 7 -> 8 (1750 опыта)
            2200,  // Уровень 8 -> 9 (2200 опыта)
            2700,  // Уровень 9 -> 10 (2700 опыта)
            3250   // Уровень 10 -> 11 (3250 опыта)
        };

        public ExperienceService(
            IExperienceRepository experienceRepository,
            IUserRepository userRepository,
            ISkinService skinService,
            IMapper mapper,
            ILogger<ExperienceService> logger)
        {
            _experienceRepository = experienceRepository;
            _userRepository = userRepository;
            _skinService = skinService;
            _mapper = mapper;
            _logger = logger;
        }

        public async Task<bool> AddExperienceAsync(string userId, int experience, string source, string description)
        {
            try
            {
                var user = await _userRepository.GetByIdAsync(userId);
                if (user == null)
                {
                    _logger.LogError($"User {userId} not found when adding experience");
                    return false;
                }

                // Получаем буст опыта от активного скина
                var experienceBoost = await _skinService.GetUserExperienceBoostAsync(userId);
                var boostedExperience = (int)Math.Round(experience * experienceBoost);

                _logger.LogInformation($"Adding experience to user {userId}: {experience} base, {boostedExperience} with boost {experienceBoost}x");

                var levelBefore = user.Level;
                var experienceBefore = user.Experience;

 
                user.Experience += boostedExperience;
                var newLevel = await CalculateLevelFromExperience(user.Experience);
                var leveledUp = newLevel > levelBefore;

                user.Level = newLevel;

                // Сохраняем пользователя
                await _userRepository.UpdateAsync(user);

                // Записываем транзакцию опыта
                var transaction = new ExperienceTransaction
                {
                    UserId = userId,
                    Experience = boostedExperience,
                    Source = source,
                    Description = description,
                    LevelBefore = levelBefore,
                    LevelAfter = user.Level,
                    LeveledUp = leveledUp
                };

                await _experienceRepository.CreateTransactionAsync(transaction);

                if (leveledUp)
                {
                    _logger.LogInformation($"🎉 User {userId} leveled up! {levelBefore} -> {user.Level} (Experience: {experienceBefore} -> {user.Experience})");
                }

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error adding experience to user {userId}: {ex.Message}");
                return false;
            }
        }

   
        public Task<int> CalculateLevelFromExperience(int experience)
        {
            // Находим максимальный уровень, который может достичь пользователь с данным опытом
            for (int level = LevelExperienceRequirements.Length - 1; level >= 1; level--)
            {
                if (experience >= LevelExperienceRequirements[level - 1])
                {
                    return Task.FromResult(level);
                }
            }
            return Task.FromResult(1); // Минимальный уровень
        }

        public Task<int> GetExperienceForNextLevel(int currentLevel)
        {
            if (currentLevel >= LevelExperienceRequirements.Length)
                return Task.FromResult(LevelExperienceRequirements[^1]);

            return Task.FromResult(LevelExperienceRequirements[currentLevel]);
        }

        public async Task<IEnumerable<ExperienceTransactionDto>> GetUserExperienceTransactionsAsync(string userId)
        {
            var transactions = await _experienceRepository.GetUserTransactionsAsync(userId);
            return _mapper.Map<IEnumerable<ExperienceTransactionDto>>(transactions);
        }

   
        public async Task<bool> FixUserLevelAsync(string userId)
        {
            try
            {
                var user = await _userRepository.GetByIdAsync(userId);
                if (user == null) return false;

                var correctLevel = await CalculateLevelFromExperience(user.Experience);

                if (user.Level != correctLevel)
                {
                    _logger.LogWarning($"Fixing user {userId} level: {user.Level} -> {correctLevel} (Experience: {user.Experience})");

                    user.Level = correctLevel;
                    await _userRepository.UpdateAsync(user);

                    return true;
                }

                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error fixing user level {userId}: {ex.Message}");
                return false;
            }
        }
    }
}
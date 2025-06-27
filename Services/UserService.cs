using FitnessTracker.API.DTOs;
using FitnessTracker.API.Repositories;
using AutoMapper;

namespace FitnessTracker.API.Services
{
    public class UserService : IUserService
    {
        private readonly IUserRepository _userRepository;
        private readonly IExperienceService _experienceService;
        private readonly IMapper _mapper;

        private static readonly int[] LevelExperienceRequirements = {
            0,     // Уровень 0 (не используется)
            100,   // Уровень 1 -> 2
            250,   // Уровень 2 -> 3
            450,   // Уровень 3 -> 4
            700,   // Уровень 4 -> 5
            1000,  // Уровень 5 -> 6
            1350,  // Уровень 6 -> 7
            1750,  // Уровень 7 -> 8
            2200,  // Уровень 8 -> 9
            2700,  // Уровень 9 -> 10
            3250,  // Уровень 10 -> 11
            3850,  // Уровень 11 -> 12
            4500,  // Уровень 12 -> 13
            5200,  // Уровень 13 -> 14
            5950,  // Уровень 14 -> 15
            6750,  // Уровень 15 -> 16
            7600,  // Уровень 16 -> 17
            8500,  // Уровень 17 -> 18
            9450,  // Уровень 18 -> 19
            10450, // Уровень 19 -> 20
            11500  // Уровень 20 -> 21
        };

        public UserService(IUserRepository userRepository, IExperienceService experienceService, IMapper mapper)
        {
            _userRepository = userRepository;
            _experienceService = experienceService;
            _mapper = mapper;
        }

        public async Task<UserDto?> GetUserByIdAsync(string userId)
        {
            var user = await _userRepository.GetByIdAsync(userId);
            if (user == null) return null;

            var userDto = _mapper.Map<UserDto>(user);

            // ✅ ДОБАВЛЕНО: Расчет данных об опыте
            var experienceData = CalculateExperienceData(user.Level, user.Experience);
            userDto.MaxExperience = experienceData.MaxExperience;
            userDto.ExperienceToNextLevel = experienceData.ExperienceToNextLevel;
            userDto.ExperienceProgress = experienceData.ExperienceProgress;

            return userDto;
        }

        public async Task<UserDto> UpdateUserProfileAsync(string userId, UpdateUserProfileRequest request)
        {
            var user = await _userRepository.GetByIdAsync(userId);
            if (user == null)
                throw new ArgumentException("User not found");

            user.Name = request.Name;
            user.Age = request.Age;
            user.Gender = request.Gender;
            user.Weight = request.Weight;
            user.Height = request.Height;

            user = await _userRepository.UpdateAsync(user);

            var userDto = _mapper.Map<UserDto>(user);

            var experienceData = CalculateExperienceData(user.Level, user.Experience);
            userDto.MaxExperience = experienceData.MaxExperience;
            userDto.ExperienceToNextLevel = experienceData.ExperienceToNextLevel;
            userDto.ExperienceProgress = experienceData.ExperienceProgress;

            return userDto;
        }

        public async Task DeleteUserAsync(string userId)
        {
            await _userRepository.DeleteAsync(userId);
        }

        private (int MaxExperience, int ExperienceToNextLevel, decimal ExperienceProgress) CalculateExperienceData(int level, int currentExperience)
        {
            // Опыт, необходимый для достижения текущего уровня
            int currentLevelMinExperience = level > 1 && level - 1 < LevelExperienceRequirements.Length
                ? LevelExperienceRequirements[level - 1]
                : 0;

            // Опыт, необходимый для достижения следующего уровня
            int nextLevelMaxExperience = level < LevelExperienceRequirements.Length
                ? LevelExperienceRequirements[level]
                : LevelExperienceRequirements[^1]; // Максимальный уровень

            // Опыт в рамках текущего уровня
            int experienceInCurrentLevel = currentExperience - currentLevelMinExperience;

            // Сколько опыта нужно для перехода на следующий уровень
            int experienceNeededForLevel = nextLevelMaxExperience - currentLevelMinExperience;

            // Сколько еще нужно опыта до следующего уровня
            int experienceToNextLevel = Math.Max(0, nextLevelMaxExperience - currentExperience);

            // Прогресс в процентах
            decimal progress = experienceNeededForLevel > 0
                ? Math.Min(100, (decimal)experienceInCurrentLevel / experienceNeededForLevel * 100)
                : 100;

            return (
                MaxExperience: nextLevelMaxExperience,
                ExperienceToNextLevel: experienceToNextLevel,
                ExperienceProgress: Math.Round(progress, 1)
            );
        }
    }
}
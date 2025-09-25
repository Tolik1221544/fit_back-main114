using FitnessTracker.API.DTOs;
using FitnessTracker.API.Models;
using FitnessTracker.API.Repositories;
using AutoMapper;
using System.IdentityModel.Tokens.Jwt;
using Microsoft.IdentityModel.Tokens;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace FitnessTracker.API.Services
{
    public interface IAppleAuthService
    {
        Task<AuthResponseDto> AuthenticateWithIdTokenAsync(string idToken, string? authorizationCode = null);
    }

    public class AppleAuthService : IAppleAuthService
    {
        private readonly IUserRepository _userRepository;
        private readonly IAuthService _authService;
        private readonly ILwCoinService _lwCoinService;
        private readonly IMapper _mapper;
        private readonly IConfiguration _configuration;
        private readonly ILogger<AppleAuthService> _logger;
        private readonly HttpClient _httpClient;

        public AppleAuthService(
            IUserRepository userRepository,
            IAuthService authService,
            ILwCoinService lwCoinService,
            IMapper mapper,
            IConfiguration configuration,
            ILogger<AppleAuthService> logger,
            HttpClient httpClient)
        {
            _userRepository = userRepository;
            _authService = authService;
            _lwCoinService = lwCoinService;
            _mapper = mapper;
            _configuration = configuration;
            _logger = logger;
            _httpClient = httpClient;
        }

        /// <summary>
        /// 🍎 Аутентификация через Apple ID token (Firebase или прямой)
        /// </summary>
        public async Task<AuthResponseDto> AuthenticateWithIdTokenAsync(string idToken, string? authorizationCode = null)
        {
            try
            {
                _logger.LogInformation($"🍎 Validating Apple ID token");

                var handler = new JwtSecurityTokenHandler();
                var jsonToken = handler.ReadJwtToken(idToken);

                var email = jsonToken.Claims.FirstOrDefault(c => c.Type == "email")?.Value;
                var sub = jsonToken.Claims.FirstOrDefault(c => c.Type == "sub")?.Value; // Apple User ID
                var emailVerified = jsonToken.Claims.FirstOrDefault(c => c.Type == "email_verified")?.Value;

                var firebaseSign = jsonToken.Claims.FirstOrDefault(c => c.Type == "firebase")?.Value;
                if (!string.IsNullOrEmpty(firebaseSign))
                {
                    _logger.LogInformation($"🍎 Token from Firebase detected");
                    email = email ?? jsonToken.Claims.FirstOrDefault(c => c.Type == "user_id")?.Value;
                }

                if (string.IsNullOrEmpty(email) && !string.IsNullOrEmpty(sub))
                {
                    email = $"{sub}@privaterelay.appleid.com";
                    _logger.LogWarning($"🍎 Email not provided, using Apple ID: {email}");
                }

                if (string.IsNullOrEmpty(email))
                {
                    throw new UnauthorizedAccessException("Unable to get email from Apple token");
                }

                _logger.LogInformation($"🍎 Apple ID token validated for email: {email}");

                var user = await GetOrCreateUserAsync(email, sub, "apple");

                var jwtToken = await _authService.GenerateJwtTokenAsync(user.Id);

                var userDto = CreateUserDto(user);

                return new AuthResponseDto
                {
                    AccessToken = jwtToken,
                    User = userDto
                };
            }
            catch (Exception ex)
            {
                _logger.LogError($"❌ Apple authentication error: {ex.Message}");
                throw new UnauthorizedAccessException($"Invalid Apple ID token: {ex.Message}");
            }
        }

        /// <summary>
        /// 👤 Создание или получение пользователя
        /// </summary>
        private async Task<User> GetOrCreateUserAsync(string email, string? appleUserId, string registeredVia)
        {
            email = email.Trim().ToLowerInvariant();

            var existingUser = await _userRepository.GetByEmailAsync(email);

            if (existingUser != null)
            {
                _logger.LogInformation($"✅ Existing user found: {email}");

                if (existingUser.LwCoins == 0 || existingUser.FractionalLwCoins == 0)
                {
                    var transactions = await _lwCoinService.GetUserLwCoinTransactionsAsync(existingUser.Id);
                    var hasRegistrationBonus = transactions.Any(t => t.CoinSource == "registration");

                    if (!hasRegistrationBonus)
                    {
                        _logger.LogWarning($"⚠️ User {email} missing registration bonus, adding now");
                        await _lwCoinService.AddRegistrationBonusAsync(existingUser.Id);
                    }
                };

                bool needsUpdate = false;

                if (!existingUser.IsEmailConfirmed)
                {
                    existingUser.IsEmailConfirmed = true;
                    needsUpdate = true;
                }

                if (string.IsNullOrEmpty(existingUser.ReferralCode))
                {
                    existingUser.ReferralCode = await GenerateUniqueReferralCodeAsync();
                    needsUpdate = true;
                }

                if (!string.IsNullOrEmpty(appleUserId) && existingUser.AppleUserId != appleUserId)
                {
                    existingUser.AppleUserId = appleUserId;
                    needsUpdate = true;
                }

                if (needsUpdate)
                {
                    await _userRepository.UpdateAsync(existingUser);
                }

                return existingUser;
            }
            else
            {
                var referralCode = await GenerateUniqueReferralCodeAsync();

                var newUser = new User
                {
                    Id = Guid.NewGuid().ToString(),
                    Email = email,
                    Name = "",
                    RegisteredVia = registeredVia,
                    AppleUserId = appleUserId,
                    Level = 1,
                    Experience = 0,
                    LwCoins = 0,  
                    FractionalLwCoins = 0.0,
                    ReferralCode = referralCode,
                    IsEmailConfirmed = true,
                    JoinedAt = DateTime.UtcNow,
                    LastMonthlyRefill = DateTime.UtcNow,
                    Age = 0,
                    Gender = "",
                    Weight = 0,
                    Height = 0,
                    Locale = "en"
                };

                var createdUser = await _userRepository.CreateAsync(newUser);

                var bonusAdded = await _lwCoinService.AddRegistrationBonusAsync(createdUser.Id);
                if (!bonusAdded)
                {
                    _logger.LogError($"❌ Failed to add registration bonus for {email}");
                    await Task.Delay(500);
                    bonusAdded = await _lwCoinService.AddRegistrationBonusAsync(createdUser.Id);
                }

                createdUser = await _userRepository.GetByIdAsync(createdUser.Id);

                _logger.LogInformation($"✅ New Apple user created: {email} with {createdUser.LwCoins} coins");
                return createdUser;
            }
        }

        private async Task<string> GenerateUniqueReferralCodeAsync()
        {
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
            var random = new Random();

            string code;
            int attempts = 0;
            do
            {
                code = new string(Enumerable.Repeat(chars, 8)
                    .Select(s => s[random.Next(s.Length)]).ToArray());
                attempts++;
            } while (await _userRepository.GetByReferralCodeAsync(code) != null && attempts < 10);

            return code;
        }

        private UserDto CreateUserDto(User user)
        {
            var userDto = _mapper.Map<UserDto>(user);

            var experienceData = CalculateExperienceData(user.Level, user.Experience);
            userDto.MaxExperience = experienceData.MaxExperience;
            userDto.ExperienceToNextLevel = experienceData.ExperienceToNextLevel;
            userDto.ExperienceProgress = experienceData.ExperienceProgress;

            return userDto;
        }

        private (int MaxExperience, int ExperienceToNextLevel, decimal ExperienceProgress) CalculateExperienceData(int level, int currentExperience)
        {
            var levelRequirements = new[] { 0, 100, 250, 450, 700, 1000, 1350, 1750, 2200, 2700, 3250 };

            int currentLevelMinExperience = level > 1 && level - 1 < levelRequirements.Length
                ? levelRequirements[level - 1]
                : 0;

            int nextLevelMaxExperience = level < levelRequirements.Length
                ? levelRequirements[level]
                : levelRequirements[^1];

            int experienceInCurrentLevel = currentExperience - currentLevelMinExperience;
            int experienceNeededForLevel = nextLevelMaxExperience - currentLevelMinExperience;
            int experienceToNextLevel = Math.Max(0, nextLevelMaxExperience - currentExperience);

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
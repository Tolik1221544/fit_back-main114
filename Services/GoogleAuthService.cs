using FitnessTracker.API.DTOs;
using FitnessTracker.API.Models;
using FitnessTracker.API.Repositories;
using Google.Apis.Auth;
using AutoMapper;

namespace FitnessTracker.API.Services
{
    public class GoogleAuthService : IGoogleAuthService
    {
        private readonly IUserRepository _userRepository;
        private readonly IAuthService _authService;
        private readonly IMapper _mapper;
        private readonly IConfiguration _configuration;
        private readonly ILogger<GoogleAuthService> _logger;

        public GoogleAuthService(
            IUserRepository userRepository,
            IAuthService authService,
            IMapper mapper,
            IConfiguration configuration,
            ILogger<GoogleAuthService> logger)
        {
            _userRepository = userRepository;
            _authService = authService;
            _mapper = mapper;
            _configuration = configuration;
            _logger = logger;
        }

        public async Task<AuthResponseDto> AuthenticateGoogleTokenAsync(string googleToken)
        {
            try
            {
                var clientId = _configuration["GoogleAuth:ClientId"];
                _logger.LogInformation($"Validating Google token with ClientId: {clientId}");

                // Verify Google token
                var payload = await GoogleJsonWebSignature.ValidateAsync(googleToken, new GoogleJsonWebSignature.ValidationSettings
                {
                    Audience = new[] { clientId }
                });

                var email = payload.Email;
                if (string.IsNullOrEmpty(email))
                {
                    throw new UnauthorizedAccessException("Unable to get email from Google token");
                }

                email = email.Trim().ToLowerInvariant();
                _logger.LogInformation($"Google token validated for email: {email}");

                // Check if user exists
                var existingUser = await _userRepository.GetByEmailAsync(email);

                User user;
                if (existingUser == null)
                {
    
                    var referralCode = await GenerateUniqueReferralCode();

                    // Create new user
                    user = new User
                    {
                        Id = Guid.NewGuid().ToString(),
                        Email = email,
                        Name = payload.Name ?? "Пользователь",
                        RegisteredVia = "google",
                        Level = 1,
                        Experience = 0,
                        LwCoins = 300,
                        ReferralCode = referralCode,
                        IsEmailConfirmed = true,
                        JoinedAt = DateTime.UtcNow,
                        LastMonthlyRefill = DateTime.UtcNow
                    };
                    user = await _userRepository.CreateAsync(user);

                    _logger.LogInformation($"New user created via Google: {email}");
                }
                else
                {
                    user = existingUser;


                    bool needsUpdate = false;

                    if (string.IsNullOrEmpty(user.Name) && !string.IsNullOrEmpty(payload.Name))
                    {
                        user.Name = payload.Name;
                        needsUpdate = true;
                    }

                    if (!user.IsEmailConfirmed)
                    {
                        user.IsEmailConfirmed = true;
                        needsUpdate = true;
                    }

                    if (string.IsNullOrEmpty(user.ReferralCode))
                    {
                        user.ReferralCode = await GenerateUniqueReferralCode();
                        needsUpdate = true;
                    }

                    if (needsUpdate)
                    {
                        await _userRepository.UpdateAsync(user);
                    }

                    _logger.LogInformation($"Existing user logged in via Google: {email}");
                }

      
                var token = await _authService.GenerateJwtTokenAsync(user.Id);

                var userDto = _mapper.Map<UserDto>(user);

    
                var experienceData = CalculateExperienceData(user.Level, user.Experience);
                userDto.MaxExperience = experienceData.MaxExperience;
                userDto.ExperienceToNextLevel = experienceData.ExperienceToNextLevel;
                userDto.ExperienceProgress = experienceData.ExperienceProgress;

                return new AuthResponseDto
                {
                    AccessToken = token,
                    User = userDto
                };
            }
            catch (InvalidJwtException ex)
            {
                _logger.LogError($"Invalid Google token: {ex.Message}");
                throw new UnauthorizedAccessException("Invalid Google token");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Google authentication error: {ex.Message}");
                throw new Exception("Google authentication failed");
            }
        }

    
        private async Task<string> GenerateUniqueReferralCode()
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
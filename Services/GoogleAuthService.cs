using FitnessTracker.API.DTOs;
using FitnessTracker.API.Models;
using FitnessTracker.API.Repositories;
using Google.Apis.Auth;
using AutoMapper;
using System.Text.Json;

namespace FitnessTracker.API.Services
{
    public class GoogleAuthService : IGoogleAuthService
    {
        private readonly IUserRepository _userRepository;
        private readonly IAuthService _authService;
        private readonly ILwCoinService _lwCoinService;
        private readonly IMapper _mapper;
        private readonly IConfiguration _configuration;
        private readonly ILogger<GoogleAuthService> _logger;
        private readonly HttpClient _httpClient;

        public GoogleAuthService(
            IUserRepository userRepository,
            IAuthService authService,
            IMapper mapper,
            IConfiguration configuration,
            ILogger<GoogleAuthService> logger,
            HttpClient httpClient)
        {
            _userRepository = userRepository;
            _authService = authService;
            _mapper = mapper;
            _configuration = configuration;
            _logger = logger;
            _httpClient = httpClient;
        }

        public async Task<AuthResponseDto> AuthenticateWithIdTokenAsync(string idToken)
        {
            try
            {
                var androidClientId = "810583785090-3alv9frm8kllkvm1bvjhs3frtmkt0tr3.apps.googleusercontent.com";
                var webClientId = "810583785090-j8c5du1shjc3auabhofnukkskroabavu.apps.googleusercontent.com";

                var configClientId = _configuration["GoogleAuth:ClientId"];

                _logger.LogInformation($"🔐 Validating Google ID token");
                _logger.LogInformation($"Android ClientId: {androidClientId}");
                _logger.LogInformation($"Web ClientId: {webClientId}");
                _logger.LogInformation($"Config ClientId: {configClientId}");

                GoogleJsonWebSignature.Payload? payload = null;
                Exception? lastException = null;

                var clientIds = new[] { androidClientId, webClientId, configClientId }.Where(id => !string.IsNullOrEmpty(id)).Distinct().ToArray();

                foreach (var clientId in clientIds)
                {
                    try
                    {
                        _logger.LogInformation($"Trying to validate with ClientId: {clientId}");

                        payload = await GoogleJsonWebSignature.ValidateAsync(idToken, new GoogleJsonWebSignature.ValidationSettings
                        {
                            Audience = new[] { clientId }
                        });

                        if (payload != null)
                        {
                            _logger.LogInformation($"✅ Token validated successfully with ClientId: {clientId}");
                            break;
                        }
                    }
                    catch (InvalidJwtException ex)
                    {
                        _logger.LogWarning($"❌ Validation failed with ClientId {clientId}: {ex.Message}");
                        lastException = ex;
                    }
                }

                if (payload == null)
                {
                    try
                    {
                        _logger.LogWarning($"⚠️ Trying to validate without audience check (less secure)");
                        payload = await GoogleJsonWebSignature.ValidateAsync(idToken);
                        _logger.LogInformation($"✅ Token validated without audience check");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError($"❌ Token validation failed completely: {ex.Message}");
                        throw new UnauthorizedAccessException($"Invalid Google ID token: {lastException?.Message ?? ex.Message}");
                    }
                }

                if (payload.EmailVerified != true)
                {
                    throw new UnauthorizedAccessException("Email not verified by Google");
                }

                var email = payload.Email;
                if (string.IsNullOrEmpty(email))
                {
                    throw new UnauthorizedAccessException("Unable to get email from Google token");
                }

                _logger.LogInformation($"✅ Google ID token validated for email: {email}");
                _logger.LogInformation($"User info - Name: {payload.Name}, Subject: {payload.Subject}");

                var user = await GetOrCreateUserAsync(email, payload.Name, "google");

                var jwtToken = await _authService.GenerateJwtTokenAsync(user.Id);

                var userDto = CreateUserDto(user);

                return new AuthResponseDto
                {
                    AccessToken = jwtToken,
                    User = userDto
                };
            }
            catch (InvalidJwtException ex)
            {
                _logger.LogError($"❌ Invalid Google ID token: {ex.Message}");
                throw new UnauthorizedAccessException($"Invalid Google ID token: {ex.Message}");
            }
            catch (Exception ex)
            {
                _logger.LogError($"❌ Google ID token authentication error: {ex.Message}");
                _logger.LogError($"Stack trace: {ex.StackTrace}");
                throw;
            }
        }

        public async Task<AuthResponseDto> AuthenticateWithServerCodeAsync(string serverAuthCode)
        {
            _logger.LogWarning($"⚠️ AuthenticateWithServerCodeAsync called but Flutter cannot provide serverAuthCode");
            throw new NotSupportedException("Flutter client cannot provide serverAuthCode. Use idToken instead.");
        }

        public async Task<AuthResponseDto> AuthenticateGoogleTokenAsync(string googleToken)
        {
            return await AuthenticateWithIdTokenAsync(googleToken);
        }

        private async Task<User> GetOrCreateUserAsync(string email, string? googleName, string registeredVia)
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
                        _logger.LogWarning($"⚠️ User {email} has no registration bonus, adding now");
                        await _lwCoinService.AddRegistrationBonusAsync(existingUser.Id);
                    }
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
                    Level = 1,
                    Experience = 0,
<<<<<<< HEAD
                    LwCoins = 50,
=======
                    LwCoins = 0,  
                    FractionalLwCoins = 0.0,
>>>>>>> 7529a9123b9f438413454aade598df630316f3c9
                    ReferralCode = referralCode,
                    IsEmailConfirmed = true,
                    JoinedAt = DateTime.UtcNow,
                    LastMonthlyRefill = DateTime.UtcNow,
                    Age = 0,
                    Gender = "",
                    Weight = 0,
                    Height = 0
                };

                var createdUser = await _userRepository.CreateAsync(newUser);

                var bonusAdded = await _lwCoinService.AddRegistrationBonusAsync(createdUser.Id);
                if (!bonusAdded)
                {
                    _logger.LogError($"❌ Failed to add registration bonus for new user {email}");
                    await Task.Delay(500);
                    bonusAdded = await _lwCoinService.AddRegistrationBonusAsync(createdUser.Id);
                    if (!bonusAdded)
                    {
                        _logger.LogError($"❌ Second attempt failed for registration bonus {email}");
                    }
                }
                createdUser = await _userRepository.GetByIdAsync(createdUser.Id);

                _logger.LogInformation($"✅ New user created via Google: {email} with {createdUser.LwCoins} coins");
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

    public class GoogleUserInfo
    {
        public string Id { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Given_Name { get; set; } = string.Empty;
        public string Family_Name { get; set; } = string.Empty;
        public string Picture { get; set; } = string.Empty;
        public bool Verified_Email { get; set; }
    }
}
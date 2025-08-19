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

        /// <summary>
        /// 🔐 Аутентификация через Google ID token (быстрый способ)
        /// </summary>
        public async Task<AuthResponseDto> AuthenticateWithIdTokenAsync(string idToken)
        {
            try
            {
                var clientId = _configuration["GoogleAuth:ClientId"];
                if (string.IsNullOrEmpty(clientId))
                    throw new InvalidOperationException("Google Client ID not configured");

                _logger.LogInformation($"🔐 Validating Google ID token with ClientId: {clientId}");

                var payload = await GoogleJsonWebSignature.ValidateAsync(idToken, new GoogleJsonWebSignature.ValidationSettings
                {
                    Audience = new[] { clientId }
                });

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
                throw new UnauthorizedAccessException("Invalid Google ID token");
            }
            catch (Exception ex)
            {
                _logger.LogError($"❌ Google ID token authentication error: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// 🔄 Аутентификация через server auth code (с получением refresh token)
        /// </summary>
        public async Task<AuthResponseDto> AuthenticateWithServerCodeAsync(string serverAuthCode)
        {
            try
            {
                if (string.IsNullOrEmpty(serverAuthCode))
                {
                    throw new UnauthorizedAccessException("Server auth code is required");
                }

                _logger.LogInformation($"Exchanging server auth code for tokens: {serverAuthCode}");

                var tokenResponse = await ExchangeCodeForTokensAsync(serverAuthCode);

                if (tokenResponse == null || string.IsNullOrEmpty(tokenResponse.access_token))
                {
                    throw new UnauthorizedAccessException("Failed to exchange server auth code for tokens");
                }

                _logger.LogInformation($"✅ Successfully exchanged server auth code for tokens");

                var userInfo = await GetGoogleUserInfoAsync(tokenResponse.access_token);

                if (userInfo == null || string.IsNullOrEmpty(userInfo.Email))
                {
                    throw new UnauthorizedAccessException("Failed to get user info from Google");
                }

                _logger.LogInformation($"✅ Retrieved user info for email: {userInfo.Email}");

                var user = await GetOrCreateUserAsync(userInfo.Email, userInfo.Name, "google");

                if (!string.IsNullOrEmpty(tokenResponse.refresh_token))
                {
                    _logger.LogInformation($"💾 Refresh token available for future Google API calls");
                    // await SaveGoogleRefreshTokenAsync(user.Id, tokenResponse.refresh_token);
                }

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
                _logger.LogError($"❌ Google server auth code authentication error: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Legacy метод для совместимости
        /// </summary>
        public async Task<AuthResponseDto> AuthenticateGoogleTokenAsync(string googleToken)
        {
            if (googleToken.Contains('.') && googleToken.Split('.').Length == 3)
            {
                return await AuthenticateWithIdTokenAsync(googleToken);
            }
            else
            {
                return await AuthenticateWithServerCodeAsync(googleToken);
            }
        }

        /// <summary>
        /// 🔄 Обмен server auth code на access_token и refresh_token
        /// </summary>
        private async Task<GoogleTokenResponse?> ExchangeCodeForTokensAsync(string serverAuthCode)
        {
            try
            {
                var clientId = _configuration["GoogleAuth:ClientId"];
                var clientSecret = _configuration["GoogleAuth:ClientSecret"];

                if (string.IsNullOrEmpty(clientId) || string.IsNullOrEmpty(clientSecret))
                {
                    throw new InvalidOperationException("Google OAuth credentials not configured");
                }

                _logger.LogInformation($"🔧 Using ClientId: {clientId}");
                _logger.LogInformation($"🔧 Server Auth Code: {serverAuthCode}");

                var tokenEndpoint = "https://oauth2.googleapis.com/token";

                var redirectUris = new[] { "", "postmessage", "urn:ietf:wg:oauth:2.0:oob" };

                foreach (var redirectUri in redirectUris)
                {
                    try
                    {
                        _logger.LogInformation($"🔄 Trying redirect_uri: '{redirectUri}'");

                        var requestData = new FormUrlEncodedContent(new[]
                        {
                            new KeyValuePair<string, string>("code", serverAuthCode),
                            new KeyValuePair<string, string>("client_id", clientId),
                            new KeyValuePair<string, string>("client_secret", clientSecret),
                            new KeyValuePair<string, string>("redirect_uri", redirectUri),
                            new KeyValuePair<string, string>("grant_type", "authorization_code")
                        });

                        var response = await _httpClient.PostAsync(tokenEndpoint, requestData);
                        var responseContent = await response.Content.ReadAsStringAsync();

                        _logger.LogInformation($"📊 Google Response Status: {response.StatusCode}");
                        _logger.LogInformation($"📊 Google Response Content: {responseContent}");

                        if (response.IsSuccessStatusCode)
                        {
                            _logger.LogInformation($"✅ SUCCESS with redirect_uri: '{redirectUri}'");

                            var tokenResponse = JsonSerializer.Deserialize<GoogleTokenResponse>(responseContent, new JsonSerializerOptions
                            {
                                PropertyNameCaseInsensitive = true
                            });

                            return tokenResponse;
                        }
                        else
                        {
                            _logger.LogWarning($"❌ FAILED with redirect_uri '{redirectUri}': {response.StatusCode}");
                            _logger.LogWarning($"📄 Error details: {responseContent}");
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError($"❌ Exception with redirect_uri '{redirectUri}': {ex.Message}");
                    }
                }

                _logger.LogError($"❌ ALL redirect_uri variants failed for server auth code: {serverAuthCode}");
                throw new UnauthorizedAccessException($"Failed to exchange server auth code '{serverAuthCode}' with all redirect_uri variants. Check logs for Google API error details.");
            }
            catch (Exception ex)
            {
                _logger.LogError($"❌ Error exchanging server auth code: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// 👤 Получение информации о пользователе через Google API
        /// </summary>
        private async Task<GoogleUserInfo?> GetGoogleUserInfoAsync(string accessToken)
        {
            try
            {
                var userInfoEndpoint = "https://www.googleapis.com/oauth2/v1/userinfo";

                _httpClient.DefaultRequestHeaders.Clear();
                _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {accessToken}");

                var response = await _httpClient.GetAsync(userInfoEndpoint);
                var responseContent = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError($"❌ Google user info request failed: {response.StatusCode} - {responseContent}");
                    return null;
                }

                var userInfo = JsonSerializer.Deserialize<GoogleUserInfo>(responseContent, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                return userInfo;
            }
            catch (Exception ex)
            {
                _logger.LogError($"❌ Error getting Google user info: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 👤 Создание или получение пользователя
        /// </summary>
        private async Task<User> GetOrCreateUserAsync(string email, string? name, string registeredVia)
        {
            email = email.Trim().ToLowerInvariant();

            var existingUser = await _userRepository.GetByEmailAsync(email);

            if (existingUser != null)
            {
                bool needsUpdate = false;

                if (string.IsNullOrEmpty(existingUser.Name) && !string.IsNullOrEmpty(name))
                {
                    existingUser.Name = name;
                    needsUpdate = true;
                }

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

                if (needsUpdate)
                {
                    await _userRepository.UpdateAsync(existingUser);
                }

                _logger.LogInformation($"✅ Existing user updated: {email}");
                return existingUser;
            }
            else
            {
                // Создаем нового пользователя
                var referralCode = await GenerateUniqueReferralCodeAsync();

                var newUser = new User
                {
                    Id = Guid.NewGuid().ToString(),
                    Email = email,
                    Name = name ?? "Пользователь",
                    RegisteredVia = registeredVia,
                    Level = 1,
                    Experience = 0,
                    LwCoins = 300,
                    FractionalLwCoins = 300.0,
                    ReferralCode = referralCode,
                    IsEmailConfirmed = true,
                    JoinedAt = DateTime.UtcNow,
                    LastMonthlyRefill = DateTime.UtcNow
                };

                var createdUser = await _userRepository.CreateAsync(newUser);

                _logger.LogInformation($"✅ New user created via Google: {email}");
                return createdUser;
            }
        }

        /// <summary>
        /// 🔧 Генерация уникального реферального кода
        /// </summary>
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

        /// <summary>
        /// 📊 Создание DTO пользователя с расчетом опыта
        /// </summary>
        private UserDto CreateUserDto(User user)
        {
            var userDto = _mapper.Map<UserDto>(user);

            var experienceData = CalculateExperienceData(user.Level, user.Experience);
            userDto.MaxExperience = experienceData.MaxExperience;
            userDto.ExperienceToNextLevel = experienceData.ExperienceToNextLevel;
            userDto.ExperienceProgress = experienceData.ExperienceProgress;

            return userDto;
        }

        /// <summary>
        /// 📈 Расчет данных опыта для уровня
        /// </summary>
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

    /// <summary>
    /// 👤 Информация о пользователе от Google API
    /// </summary>
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
using FitnessTracker.API.DTOs;
using FitnessTracker.API.Repositories;
using FitnessTracker.API.Models;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Collections.Concurrent;
using AutoMapper;

namespace FitnessTracker.API.Services
{
    public class AuthService : IAuthService
    {
        private readonly IUserRepository _userRepository;
        private readonly IEmailService _emailService;
        private readonly IConfiguration _configuration;
        private readonly IMapper _mapper;
        private readonly ILogger<AuthService> _logger;

        private static readonly string JWT_SECRET_KEY = "fitness-tracker-super-secret-key-that-is-definitely-long-enough-for-security-2024";
        private static readonly ConcurrentDictionary<string, (string Code, DateTime Expiry)> _verificationCodes = new();

        private static readonly Dictionary<string, string> TestCodes = new()
        {
            { "test@lightweightfit.com", "123456" },
            { "demo@lightweightfit.com", "111111" },
            { "review@lightweightfit.com", "777777" },
            { "apple.review@lightweightfit.com", "999999" },
            { "dev@lightweightfit.com", "000000" }
        };

        public AuthService(
            IUserRepository userRepository,
            IEmailService emailService,
            IConfiguration configuration,
            IMapper mapper,
            ILogger<AuthService> logger)
        {
            _userRepository = userRepository;
            _emailService = emailService;
            _configuration = configuration;
            _mapper = mapper;
            _logger = logger;
        }

        public async Task<bool> SendVerificationCodeAsync(string email)
        {
            try
            {
                email = email.Trim().ToLowerInvariant();
                _logger.LogInformation($"📧 Sending verification code to {email}");

                if (TestCodes.ContainsKey(email))
                {
                    var testCode = TestCodes[email];

                    _verificationCodes.AddOrUpdate(email,
                        (testCode, DateTime.UtcNow.AddMinutes(30)), 
                        (key, oldValue) => (testCode, DateTime.UtcNow.AddMinutes(30)));

                    _logger.LogInformation($"✅ Fixed code provided for: {email}");
                    Console.WriteLine("==================================================");
                    Console.WriteLine($"🔑 FIXED CODE FOR TESTING");
                    Console.WriteLine($"📧 Email: {email}");
                    Console.WriteLine($"🔐 Code: {testCode}");
                    Console.WriteLine($"⏰ Valid for 30 minutes");
                    Console.WriteLine($"✅ Use this code in /api/auth/confirm-email");
                    Console.WriteLine("==================================================");

                    return true;
                }

                var code = new Random().Next(100000, 999999).ToString();
                CleanExpiredCodes();

                _verificationCodes.AddOrUpdate(email,
                    (code, DateTime.UtcNow.AddMinutes(5)),
                    (key, oldValue) => (code, DateTime.UtcNow.AddMinutes(5)));

                var result = await _emailService.SendVerificationEmailAsync(email, code);
                _logger.LogInformation($"Verification code sent to {email}: {(result ? "success" : "failed")}");
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error sending verification code to {email}: {ex.Message}");
                return false;
            }
        }

        public async Task<AuthResponseDto> ConfirmEmailAsync(string email, string code)
        {
            try
            {
                email = email.Trim().ToLowerInvariant();
                _logger.LogInformation($"📧 Confirming email for {email}");

                if (!_verificationCodes.TryGetValue(email, out var storedData))
                {
                    _logger.LogWarning($"No verification code found for {email}");
                    throw new UnauthorizedAccessException("No verification code found for this email");
                }

                if (storedData.Code != code.Trim())
                {
                    _logger.LogWarning($"Invalid verification code for {email}. Expected: {storedData.Code}, Got: {code}");
                    throw new UnauthorizedAccessException("Invalid verification code");
                }

                if (DateTime.UtcNow > storedData.Expiry)
                {
                    _logger.LogWarning($"Expired verification code for {email}");
                    _verificationCodes.TryRemove(email, out _);
                    throw new UnauthorizedAccessException("Verification code has expired");
                }

                _verificationCodes.TryRemove(email, out _);

                var existingUser = await _userRepository.GetByEmailAsync(email);
                User user;

                if (existingUser == null)
                {
                    _logger.LogInformation($"Creating new user for {email}");
                    var referralCode = await GenerateUniqueReferralCode();

                    user = new User
                    {
                        Id = Guid.NewGuid().ToString(),
                        Email = email,
                        Name = GetUserNameForEmail(email), 
                        RegisteredVia = "email",
                        Level = 1,
                        Experience = 0,
                        LwCoins = 300,
                        FractionalLwCoins = 300.0,
                        ReferralCode = referralCode,
                        JoinedAt = DateTime.UtcNow,
                        LastMonthlyRefill = DateTime.UtcNow,
                        IsEmailConfirmed = true
                    };

                    try
                    {
                        user = await _userRepository.CreateAsync(user);
                        _logger.LogInformation($"User created successfully: {user.Id}");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError($"Error creating user: {ex.Message}");
                        throw new InvalidOperationException("Failed to create user account");
                    }
                }
                else
                {
                    user = existingUser;
                    _logger.LogInformation($"Existing user found: {user.Id}");

                    bool needsUpdate = false;

                    if (!user.IsEmailConfirmed)
                    {
                        user.IsEmailConfirmed = true;
                        needsUpdate = true;
                    }

                    if (string.IsNullOrEmpty(user.Name))
                    {
                        user.Name = GetUserNameForEmail(email);
                        needsUpdate = true;
                    }

                    if (string.IsNullOrEmpty(user.ReferralCode))
                    {
                        user.ReferralCode = await GenerateUniqueReferralCode();
                        needsUpdate = true;
                    }

                    if (needsUpdate)
                    {
                        try
                        {
                            await _userRepository.UpdateAsync(user);
                            _logger.LogInformation($"User updated: {user.Id}");
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError($"Error updating user: {ex.Message}");
                        }
                    }
                }

                string token;
                try
                {
                    token = GenerateJwtToken(user.Id);
                    _logger.LogInformation($"JWT token generated successfully for user {user.Id}");
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Error generating JWT token: {ex.Message}");
                    throw new InvalidOperationException("Failed to generate authentication token");
                }

                var userDto = _mapper.Map<UserDto>(user);
                var experienceData = CalculateExperienceData(user.Level, user.Experience);
                userDto.MaxExperience = experienceData.MaxExperience;
                userDto.ExperienceToNextLevel = experienceData.ExperienceToNextLevel;
                userDto.ExperienceProgress = experienceData.ExperienceProgress;

                if (TestCodes.ContainsKey(email))
                {
                    _logger.LogInformation($"🔑 Fixed code login successful: {email}");
                }

                return new AuthResponseDto
                {
                    AccessToken = token,
                    User = userDto
                };
            }
            catch (UnauthorizedAccessException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Unexpected error during email confirmation for {email}: {ex.Message}");
                throw new InvalidOperationException("Authentication failed due to server error");
            }
        }

        public Task<AuthResponseDto> GoogleAuthAsync(string googleToken)
        {
            throw new NotImplementedException("Google authentication not implemented yet");
        }

        public Task<bool> LogoutAsync(string accessToken)
        {
            return Task.FromResult(true);
        }

        public async Task<string> GenerateJwtTokenAsync(string userId)
        {
            return await Task.FromResult(GenerateJwtToken(userId));
        }

        private string GenerateJwtToken(string userId)
        {
            try
            {
                var key = Encoding.UTF8.GetBytes(JWT_SECRET_KEY);

                var tokenDescriptor = new SecurityTokenDescriptor
                {
                    Subject = new ClaimsIdentity(new[]
                    {
                        new Claim(ClaimTypes.NameIdentifier, userId),
                        new Claim("user_id", userId),
                        new Claim("jti", Guid.NewGuid().ToString()),
                        new Claim("iat", DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer64)
                    }),
                    Expires = DateTime.UtcNow.AddDays(30),
                    Issuer = "FitnessTracker",
                    Audience = "FitnessTracker",
                    SigningCredentials = new SigningCredentials(
                        new SymmetricSecurityKey(key),
                        SecurityAlgorithms.HmacSha256Signature)
                };

                var tokenHandler = new JwtSecurityTokenHandler();
                var token = tokenHandler.CreateToken(tokenDescriptor);
                var tokenString = tokenHandler.WriteToken(token);

                _logger.LogInformation($"JWT token generated for user {userId}");
                return tokenString;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error generating JWT token for user {userId}: {ex.Message}");
                throw;
            }
        }

        private string GetUserNameForEmail(string email)
        {
            if (TestCodes.ContainsKey(email))
            {
                return email switch
                {
                    "test@lightweightfit.com" => "Test User",
                    "demo@lightweightfit.com" => "Demo User",
                    "review@lightweightfit.com" => "Review User",
                    "apple.review@lightweightfit.com" => "Apple Review",
                    "dev@lightweightfit.com" => "Developer",
                    _ => "Test User"
                };
            }

            var namePart = email.Split('@')[0];

            namePart = namePart.Replace(".", " ");

            if (!string.IsNullOrEmpty(namePart))
            {
                var words = namePart.Split(' ');
                for (int i = 0; i < words.Length; i++)
                {
                    if (!string.IsNullOrEmpty(words[i]))
                    {
                        words[i] = char.ToUpper(words[i][0]) + words[i].Substring(1).ToLower();
                    }
                }
                namePart = string.Join(" ", words);
            }

            return string.IsNullOrEmpty(namePart) ? "Пользователь" : namePart;
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

        private void CleanExpiredCodes()
        {
            var expiredKeys = _verificationCodes
                .Where(kvp => DateTime.UtcNow > kvp.Value.Expiry)
                .Select(kvp => kvp.Key)
                .ToList();

            foreach (var key in expiredKeys)
            {
                _verificationCodes.TryRemove(key, out _);
            }
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
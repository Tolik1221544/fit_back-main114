using FitnessTracker.API.DTOs;
using FitnessTracker.API.Services;
using FitnessTracker.API.Models;
using FitnessTracker.API.Repositories;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using AutoMapper;

namespace FitnessTracker.API.Controllers
{
    [ApiController]
    [Route("api/auth")]
    public class AuthController : ControllerBase
    {
        private readonly IAuthService _authService;
        private readonly IGoogleAuthService _googleAuthService;
        private readonly IUserRepository _userRepository;
        private readonly IActivityService _activityService;
        private readonly IFoodIntakeService _foodIntakeService;
        private readonly IMapper _mapper;
        private readonly ILogger<AuthController> _logger;

        public AuthController(
            IAuthService authService,
            IGoogleAuthService googleAuthService,
            IUserRepository userRepository,
            IActivityService activityService,
            IFoodIntakeService foodIntakeService,
            IMapper mapper,
            ILogger<AuthController> logger)
        {
            _authService = authService;
            _googleAuthService = googleAuthService;
            _userRepository = userRepository;
            _activityService = activityService;
            _foodIntakeService = foodIntakeService;
            _mapper = mapper;
            _logger = logger;
        }

        [HttpPost("send-code")]
        public async Task<IActionResult> SendVerificationCode([FromBody] SendVerificationCodeRequest request)
        {
            try
            {
                var result = await _authService.SendVerificationCodeAsync(request.Email);
                return Ok(new
                {
                    success = result,
                    message = result ? "Verification code sent successfully" : "Failed to send code",
                    email = request.Email
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        [HttpPost("confirm-email")]
        public async Task<IActionResult> ConfirmEmail([FromBody] ConfirmEmailRequest request)
        {
            try
            {
                var result = await _authService.ConfirmEmailAsync(request.Email, request.Code);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        /// <summary>
        /// 🍎 Демо-вход для Apple App Review Team
        /// </summary>
        /// <param name="request">Демо-данные для входа</param>
        /// <returns>JWT токен и данные пользователя</returns>
        /// <response code="200">Демо-вход успешен</response>
        /// <response code="400">Неверные демо-данные</response>
        [HttpPost("demo-login")]
        [AllowAnonymous]
        [ProducesResponseType(typeof(AuthResponseDto), 200)]
        [ProducesResponseType(400)]
        public async Task<IActionResult> DemoLogin([FromBody] DemoLoginRequest request)
        {
            try
            {
                _logger.LogInformation($"🍎 Demo login attempt for: {request.Email}");

                if (!IsValidDemoCredentials(request.Email, request.Password))
                {
                    _logger.LogWarning($"🍎 Invalid demo credentials for: {request.Email}");
                    return BadRequest(new { error = "Invalid demo credentials" });
                }

                var user = await GetOrCreateDemoUser(request.Email);

                var token = await _authService.GenerateJwtTokenAsync(user.Id);

                var userDto = _mapper.Map<UserDto>(user);

                var experienceData = CalculateExperienceData(user.Level, user.Experience);
                userDto.MaxExperience = experienceData.MaxExperience;
                userDto.ExperienceToNextLevel = experienceData.ExperienceToNextLevel;
                userDto.ExperienceProgress = experienceData.ExperienceProgress;

                _logger.LogInformation($"✅ Demo login successful for: {request.Email}");

                return Ok(new AuthResponseDto
                {
                    AccessToken = token,
                    User = userDto
                });
            }
            catch (Exception ex)
            {
                _logger.LogError($"❌ Demo login error: {ex.Message}");
                return BadRequest(new { error = "Demo login failed" });
            }
        }

        /// <summary>
        /// Google OAuth authentication with ID token ONLY
        /// </summary>
        /// <param name="request">Google authentication data with idToken</param>
        /// <returns>Authentication result with JWT token</returns>
        /// <response code="200">Authentication successful</response>
        /// <response code="400">Invalid data or Google token validation error</response>
        /// <response code="401">Token is invalid</response>
        [HttpPost("google")]
        [ProducesResponseType(typeof(AuthResponseDto), 200)]
        [ProducesResponseType(400)]
        [ProducesResponseType(401)]
        public async Task<IActionResult> GoogleAuth([FromBody] GoogleAuthRequest request)
        {
            try
            {
                if (string.IsNullOrEmpty(request.IdToken))
                {
                    _logger.LogWarning("No idToken provided in Google auth request");
                    return BadRequest(new { error = "idToken is required" });
                }

                _logger.LogInformation($"Processing Google ID token authentication");
                _logger.LogInformation($"ID Token length: {request.IdToken.Length}");

                var result = await _googleAuthService.AuthenticateWithIdTokenAsync(request.IdToken);

                _logger.LogInformation($"Google authentication successful for user: {result.User.Email}");

                return Ok(result);
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogWarning($"Google authentication failed: {ex.Message}");
                return Unauthorized(new { error = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError($"Google authentication error: {ex.Message}");
                _logger.LogError($"Stack trace: {ex.StackTrace}");
                return BadRequest(new { error = ex.Message });
            }
        }

        [HttpPost("logout")]
        public async Task<IActionResult> Logout([FromBody] LogoutRequest request)
        {
            try
            {
                var result = await _authService.LogoutAsync(request.AccessToken);
                return Ok(new { success = result });
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        /// <summary>
        /// Validate current token (for debugging)
        /// </summary>
        [HttpGet("validate-token")]
        [Authorize]
        public IActionResult ValidateToken()
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                var email = User.FindFirst(ClaimTypes.Email)?.Value;

                return Ok(new
                {
                    valid = true,
                    userId = userId,
                    email = email,
                    claims = User.Claims.Select(c => new { c.Type, c.Value }).ToList()
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        #region Demo Account Logic

        private bool IsValidDemoCredentials(string email, string password)
        {
            if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password))
                return false;

            var demoAccounts = new Dictionary<string, string>
            {
                { "apple.review@lightweightfit.com", "AppleDemo2024!" },
                { "demo@lightweightfit.com", "Demo123456!" },
                { "test@lightweightfit.com", "Test123456!" },
                { "review@lightweightfit.com", "Review123!" },
                { "appstore@lightweightfit.com", "AppStore2024!" }
            };

            return demoAccounts.ContainsKey(email.ToLowerInvariant()) &&
                   demoAccounts[email.ToLowerInvariant()] == password;
        }

        private async Task<User> GetOrCreateDemoUser(string email)
        {
            var user = await _userRepository.GetByEmailAsync(email);

            if (user == null)
            {
                _logger.LogInformation($"🍎 Creating new demo user for: {email}");

                user = new User
                {
                    Id = Guid.NewGuid().ToString(),
                    Email = email,
                    Name = GetDemoUserName(email),
                    RegisteredVia = "demo",
                    Level = 5, 
                    Experience = 750, 
                    LwCoins = 500, 
                    FractionalLwCoins = 500.0,
                    Age = 28,
                    Gender = "мужской",
                    Weight = 75,
                    Height = 180,
                    ReferralCode = await GenerateUniqueReferralCode(),
                    IsEmailConfirmed = true,
                    JoinedAt = DateTime.UtcNow.AddDays(-30), 
                    LastMonthlyRefill = DateTime.UtcNow,
                    Locale = "en_US"
                };

                user = await _userRepository.CreateAsync(user);

                _ = Task.Run(async () => await CreateDemoDataAsync(user.Id));

                _logger.LogInformation($"✅ Demo user created: {user.Id}");
            }
            else
            {
                _logger.LogInformation($"🍎 Existing demo user found: {email}");
            }

            return user;
        }

        private string GetDemoUserName(string email)
        {
            return email.ToLowerInvariant() switch
            {
                "apple.review@lightweightfit.com" => "Apple Review Demo",
                "demo@lightweightfit.com" => "Demo User",
                "test@lightweightfit.com" => "Test User",
                "review@lightweightfit.com" => "Review User",
                "appstore@lightweightfit.com" => "App Store Demo",
                _ => "Demo User"
            };
        }

        private async Task CreateDemoDataAsync(string userId)
        {
            try
            {
                _logger.LogInformation($"🍎 Creating demo data for user: {userId}");

                var demoActivities = new[]
                {
                    new AddActivityRequest
                    {
                        Type = "cardio",
                        StartDate = DateTime.UtcNow.AddDays(-2),
                        EndDate = DateTime.UtcNow.AddDays(-2).AddMinutes(30),
                        Calories = 300,
                        ActivityData = new ActivityDataDto
                        {
                            Name = "Morning Run",
                            Category = "Cardio",
                            Distance = 5.0m,
                            AvgPace = "6:00 min/km",
                            AvgPulse = 140,
                            MaxPulse = 165
                        }
                    },
                    new AddActivityRequest
                    {
                        Type = "strength",
                        StartDate = DateTime.UtcNow.AddDays(-1),
                        EndDate = DateTime.UtcNow.AddDays(-1).AddMinutes(45),
                        Calories = 250,
                        ActivityData = new ActivityDataDto
                        {
                            Name = "Push-ups Workout",
                            Category = "Strength",
                            MuscleGroup = "грудь",
                            RestTimeSeconds = 90,
                            Sets = new List<ActivitySetDto>
                            {
                                new() { SetNumber = 1, Reps = 15, IsCompleted = true },
                                new() { SetNumber = 2, Reps = 12, IsCompleted = true },
                                new() { SetNumber = 3, Reps = 10, IsCompleted = true }
                            }
                        }
                    },
                    new AddActivityRequest
                    {
                        Type = "cardio",
                        StartDate = DateTime.UtcNow.AddHours(-3),
                        EndDate = DateTime.UtcNow.AddHours(-3).AddMinutes(20),
                        Calories = 180,
                        ActivityData = new ActivityDataDto
                        {
                            Name = "Cycling",
                            Category = "Cardio",
                            Distance = 8.0m,
                            AvgPace = "4:30 min/km",
                            Equipment = "Stationary bike"
                        }
                    }
                };

                foreach (var activity in demoActivities)
                {
                    await _activityService.AddActivityAsync(userId, activity);
                }

                var demoFoodItems = new[]
                {
                    new AddFoodIntakeRequest
                    {
                        Items = new List<FoodItemRequest>
                        {
                            new()
                            {
                                Name = "Healthy Breakfast Bowl",
                                Weight = 250,
                                WeightType = "g",
                                NutritionPer100g = new NutritionPer100gDto
                                {
                                    Calories = 200,
                                    Proteins = 12,
                                    Fats = 8,
                                    Carbs = 25
                                }
                            }
                        },
                        DateTime = DateTime.UtcNow.AddHours(-2)
                    },
                    new AddFoodIntakeRequest
                    {
                        Items = new List<FoodItemRequest>
                        {
                            new()
                            {
                                Name = "Protein Shake",
                                Weight = 300,
                                WeightType = "ml",
                                NutritionPer100g = new NutritionPer100gDto
                                {
                                    Calories = 120,
                                    Proteins = 25,
                                    Fats = 2,
                                    Carbs = 5
                                }
                            }
                        },
                        DateTime = DateTime.UtcNow.AddDays(-1).AddHours(8)
                    },
                    new AddFoodIntakeRequest
                    {
                        Items = new List<FoodItemRequest>
                        {
                            new()
                            {
                                Name = "Grilled Chicken Salad",
                                Weight = 350,
                                WeightType = "g",
                                NutritionPer100g = new NutritionPer100gDto
                                {
                                    Calories = 180,
                                    Proteins = 22,
                                    Fats = 6,
                                    Carbs = 8
                                }
                            }
                        },
                        DateTime = DateTime.UtcNow.AddDays(-1).AddHours(13)
                    }
                };

                foreach (var food in demoFoodItems)
                {
                    await _foodIntakeService.AddFoodIntakeAsync(userId, food);
                }

                var demoStepsData = new[]
                {
                    new AddStepsRequest { Steps = 8500, Calories = 300, Date = DateTime.UtcNow.Date },
                    new AddStepsRequest { Steps = 12000, Calories = 420, Date = DateTime.UtcNow.AddDays(-1).Date },
                    new AddStepsRequest { Steps = 6800, Calories = 240, Date = DateTime.UtcNow.AddDays(-2).Date }
                };

                foreach (var steps in demoStepsData)
                {
                    await _activityService.AddStepsAsync(userId, steps);
                }

                _logger.LogInformation($"✅ Demo data created successfully for user: {userId}");
            }
            catch (Exception ex)
            {
                _logger.LogError($"❌ Error creating demo data for user {userId}: {ex.Message}");
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

        #endregion
    }

    public class DemoLoginRequest
    {
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }
}
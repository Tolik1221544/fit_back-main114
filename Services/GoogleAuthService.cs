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

                // Check if user exists
                var existingUser = await _userRepository.GetByEmailAsync(email);

                User user;
                if (existingUser == null)
                {
                    // Create new user
                    user = new User
                    {
                        Email = email,
                        RegisteredVia = "google",
                        Level = 1,
                        Coins = 100,
                        LwCoins = 300, // Initial LW Coins
                        JoinedAt = DateTime.UtcNow,
                        LastMonthlyRefill = DateTime.UtcNow
                    };
                    user = await _userRepository.CreateAsync(user);

                    _logger.LogInformation($"New user created via Google: {email}");
                }
                else
                {
                    user = existingUser;
                    _logger.LogInformation($"Existing user logged in via Google: {email}");
                }

                // Generate JWT token
                var token = await _authService.GenerateJwtTokenAsync(user.Id);

                return new AuthResponseDto
                {
                    AccessToken = token,
                    User = _mapper.Map<UserDto>(user)
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
    }
}
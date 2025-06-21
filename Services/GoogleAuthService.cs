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
        private readonly IConfiguration _configuration;
        private readonly IMapper _mapper;
        private readonly ILogger<GoogleAuthService> _logger;

        public GoogleAuthService(
            IUserRepository userRepository,
            IAuthService authService,
            IConfiguration configuration,
            IMapper mapper,
            ILogger<GoogleAuthService> logger)
        {
            _userRepository = userRepository;
            _authService = authService;
            _configuration = configuration;
            _mapper = mapper;
            _logger = logger;
        }

        public async Task<AuthResponseDto> AuthenticateGoogleTokenAsync(string googleToken)
        {
            try
            {
                var clientId = _configuration["GoogleAuth:ClientId"];

                // Проверяем Google токен
                var payload = await GoogleJsonWebSignature.ValidateAsync(googleToken, new GoogleJsonWebSignature.ValidationSettings
                {
                    Audience = new[] { clientId }
                });

                _logger.LogInformation($"Google auth successful for: {payload.Email}");

                // Проверяем существует ли пользователь
                var existingUser = await _userRepository.GetByEmailAsync(payload.Email);

                User user;
                if (existingUser == null)
                {
                    // Создаем нового пользователя
                    user = new User
                    {
                        Email = payload.Email,
                        RegisteredVia = "google",
                        Level = 1,
                        Coins = 100,
                        JoinedAt = DateTime.UtcNow
                    };
                    user = await _userRepository.CreateAsync(user);
                    _logger.LogInformation($"Created new Google user: {payload.Email}");
                }
                else
                {
                    user = existingUser;
                    _logger.LogInformation($"Existing Google user logged in: {payload.Email}");
                }

                // Генерируем JWT токен
                var jwtToken = await _authService.GenerateJwtTokenAsync(user.Id);

                return new AuthResponseDto
                {
                    AccessToken = jwtToken,
                    User = _mapper.Map<UserDto>(user)
                };
            }
            catch (Exception ex)
            {
                _logger.LogError($"Google authentication failed: {ex.Message}");
                throw new UnauthorizedAccessException("Invalid Google token");
            }
        }
    }
}
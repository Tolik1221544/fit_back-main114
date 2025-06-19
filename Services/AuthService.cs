using FitnessTracker.API.DTOs;
using FitnessTracker.API.Models;
using FitnessTracker.API.Repositories;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using AutoMapper;

namespace FitnessTracker.API.Services
{
    public class AuthService : IAuthService
    {
        private readonly IUserRepository _userRepository;
        private readonly IConfiguration _configuration;
        private readonly IMapper _mapper;
        private static readonly Dictionary<string, (string Code, DateTime Expiry)> _verificationCodes = new();
        private const string StubCode = "000000"; // Заглушечный код

        public AuthService(IUserRepository userRepository, IConfiguration configuration, IMapper mapper)
        {
            _userRepository = userRepository;
            _configuration = configuration;
            _mapper = mapper;
        }

        public async Task<bool> SendVerificationCodeAsync(string email)
        {
            var code = GenerateVerificationCode();
            _verificationCodes[email] = (code, DateTime.UtcNow.AddMinutes(10));
            Console.WriteLine($"Verification code for {email}: {code}");
            return await Task.FromResult(true);
        }

        public async Task<AuthResponse> ConfirmEmailAsync(string email, string code)
        {
            // Если код — "000000", всегда пропускаем без проверки
            if (code == "000000")
            {
                var mockUser = new User
                {
                    Id = Guid.NewGuid().ToString(),
                    Email = email,
                    PasswordHash = string.Empty,
                    RegisteredVia = "mock"
                };

                var mockToken = await GenerateJwtTokenAsync(mockUser.Id);

                return new AuthResponse
                {
                    AccessToken = mockToken,
                    RefreshToken = mockToken,
                    User = new UserDto { Id = mockUser.Id, Email = email }
                };
            }

            // Обычная проверка кода из словаря
            if (!_verificationCodes.TryGetValue(email, out var storedData) ||
                storedData.Code != code ||
                storedData.Expiry < DateTime.UtcNow)
            {
                throw new UnauthorizedAccessException("Invalid or expired verification code");
            }

            // Заглушка-пользователь (без обращения к БД)
            var user = new User
            {
                Id = Guid.NewGuid().ToString(),
                Email = email,
                PasswordHash = string.Empty,
                RegisteredVia = "mock"
            };

            _verificationCodes.Remove(email);

            var token = await GenerateJwtTokenAsync(user.Id);

            return new AuthResponse
            {
                AccessToken = token,
                RefreshToken = token,
                User = new UserDto { Id = user.Id, Email = email }
            };
        }

        private async Task<User> GetOrCreateUserAsync(string email, string registeredVia)
        {
            var existingUser = await _userRepository.GetByEmailAsync(email);
            if (existingUser != null) return existingUser;

            var newUser = new User
            {
                Email = email,
                PasswordHash = string.Empty,
                RegisteredVia = registeredVia
            };
            return await _userRepository.CreateAsync(newUser);
        }

        public async Task<AuthResponse> GoogleAuthAsync(string googleToken)
        {
            var email = "user@google.com"; // заглушка
            var user = await GetOrCreateUserAsync(email, "google");

            var token = await GenerateJwtTokenAsync(user.Id);
            var userDto = _mapper.Map<UserDto>(user);

            return new AuthResponse
            {
                AccessToken = token,
                RefreshToken = token,
                User = userDto
            };
        }

        public async Task<bool> LogoutAsync(string accessToken)
        {
            return await Task.FromResult(true);
        }

        public async Task<string> GenerateJwtTokenAsync(string userId)
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var key = Encoding.ASCII.GetBytes(_configuration["Jwt:Key"] ?? "your-super-secret-key-that-is-at-least-32-characters-long");

            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(new[]
                {
                    new Claim(ClaimTypes.NameIdentifier, userId)
                }),
                Expires = DateTime.UtcNow.AddDays(7),
                SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
            };

            var token = tokenHandler.CreateToken(tokenDescriptor);
            return await Task.FromResult(tokenHandler.WriteToken(token));
        }

        private string GenerateVerificationCode()
        {
            var random = new Random();
            return random.Next(100000, 999999).ToString();
        }
    }
}
using FitnessTracker.API.DTOs;
using FitnessTracker.API.Repositories;
using FitnessTracker.API.Models;
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
        private readonly IEmailService _emailService;
        private readonly IConfiguration _configuration;
        private readonly IMapper _mapper;
        private static readonly Dictionary<string, (string Code, DateTime Expiry)> _verificationCodes = new();

        public AuthService(
            IUserRepository userRepository,
            IEmailService emailService,
            IConfiguration configuration,
            IMapper mapper)
        {
            _userRepository = userRepository;
            _emailService = emailService;
            _configuration = configuration;
            _mapper = mapper;
        }

        public async Task<bool> SendVerificationCodeAsync(string email)
        {
            try
            {
                // Generate 6-digit code
                var code = new Random().Next(100000, 999999).ToString();

                // Store code with expiry (5 minutes)
                _verificationCodes[email] = (code, DateTime.UtcNow.AddMinutes(5));

                // Send email
                return await _emailService.SendVerificationEmailAsync(email, code);
            }
            catch (Exception)
            {
                return false;
            }
        }

        public async Task<AuthResponseDto> ConfirmEmailAsync(string email, string code)
        {
            // Check if code exists and is valid
            if (!_verificationCodes.TryGetValue(email, out var storedData) ||
                storedData.Code != code ||
                DateTime.UtcNow > storedData.Expiry)
            {
                throw new UnauthorizedAccessException("Invalid or expired verification code");
            }

            // Remove used code
            _verificationCodes.Remove(email);

            // Check if user exists
            var existingUser = await _userRepository.GetByEmailAsync(email);

            User user;
            if (existingUser == null)
            {
                // Create new user
                user = new User
                {
                    Email = email,
                    RegisteredVia = "email",
                    Level = 1,
                    Coins = 100,
                    LwCoins = 300, // Initial LW Coins allowance
                    JoinedAt = DateTime.UtcNow,
                    LastMonthlyRefill = DateTime.UtcNow
                };
                user = await _userRepository.CreateAsync(user);
            }
            else
            {
                user = existingUser;
            }

            // Generate JWT token
            var token = await GenerateJwtTokenAsync(user.Id);

            return new AuthResponseDto
            {
                AccessToken = token,
                User = _mapper.Map<UserDto>(user)
            };
        }

        public async Task<AuthResponseDto> GoogleAuthAsync(string googleToken)
        {
            // TODO: Implement Google token validation
            // For now, return mock response
            throw new NotImplementedException("Google authentication not implemented yet");
        }

        public async Task<bool> LogoutAsync(string accessToken)
        {
            // In a real application, you might want to blacklist the token
            // For now, just return true
            return true;
        }

        public async Task<string> GenerateJwtTokenAsync(string userId)
        {
            var jwtKey = _configuration["Jwt:Key"] ?? "your-super-secret-key-that-is-at-least-32-characters-long";
            var key = Encoding.ASCII.GetBytes(jwtKey);

            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(new[]
                {
                    new Claim(ClaimTypes.NameIdentifier, userId)
                }),
                Expires = DateTime.UtcNow.AddDays(30),
                SigningCredentials = new SigningCredentials(
                    new SymmetricSecurityKey(key),
                    SecurityAlgorithms.HmacSha256Signature)
            };

            var tokenHandler = new JwtSecurityTokenHandler();
            var token = tokenHandler.CreateToken(tokenDescriptor);
            return tokenHandler.WriteToken(token);
        }
    }
}
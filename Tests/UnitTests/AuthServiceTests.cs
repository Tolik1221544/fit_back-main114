using Xunit;
using Moq;
using FluentAssertions;
using FitnessTracker.API.Services;
using FitnessTracker.API.Repositories;
using FitnessTracker.API.Models;
using FitnessTracker.API.DTOs;
using AutoMapper;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;

namespace FitnessTracker.API.Tests.UnitTests
{
    public class AuthServiceTests
    {
        private readonly Mock<IUserRepository> _userRepositoryMock;
        private readonly Mock<IEmailService> _emailServiceMock;
        private readonly Mock<ILwCoinService> _lwCoinServiceMock;
        private readonly Mock<IConfiguration> _configurationMock;
        private readonly IMapper _mapper; // 🔥 Реальный mapper
        private readonly Mock<ILogger<AuthService>> _loggerMock;
        private readonly Mock<IActivityService> _activityServiceMock;
        private readonly Mock<IFoodIntakeService> _foodIntakeServiceMock;
        private readonly AuthService _authService;

        public AuthServiceTests()
        {
            _userRepositoryMock = new Mock<IUserRepository>();
            _emailServiceMock = new Mock<IEmailService>();
            _lwCoinServiceMock = new Mock<ILwCoinService>();
            _configurationMock = new Mock<IConfiguration>();
            _loggerMock = new Mock<ILogger<AuthService>>();
            _activityServiceMock = new Mock<IActivityService>();
            _foodIntakeServiceMock = new Mock<IFoodIntakeService>();

            // 🔥 Создаём реальный AutoMapper с конфигурацией
            var mapperConfig = new MapperConfiguration(cfg =>
            {
                cfg.CreateMap<User, UserDto>();
                // Добавьте другие маппинги если нужно
            });
            _mapper = mapperConfig.CreateMapper();

            _authService = new AuthService(
                _userRepositoryMock.Object,
                _emailServiceMock.Object,
                _lwCoinServiceMock.Object,
                _configurationMock.Object,
                _mapper, // Используем реальный mapper
                _loggerMock.Object,
                _activityServiceMock.Object,
                _foodIntakeServiceMock.Object
            );
        }

        [Fact]
        public async Task SendVerificationCodeAsync_WithValidEmail_ReturnsTrue()
        {
            // Arrange
            var email = "test@example.com";
            _emailServiceMock.Setup(x => x.SendVerificationEmailAsync(email, It.IsAny<string>()))
                .ReturnsAsync(true);

            // Act
            var result = await _authService.SendVerificationCodeAsync(email);

            // Assert
            result.Should().BeTrue();
            _emailServiceMock.Verify(x => x.SendVerificationEmailAsync(email, It.IsAny<string>()), Times.Once);
        }

        [Fact]
        public async Task ConfirmEmailAsync_WithTestAccount_CreatesUserAndAddsBonus()
        {
            // Arrange
            var email = "test@lightweightfit.com";
            var code = "123456";
            var userId = Guid.NewGuid().ToString();

            var user = new User
            {
                Id = userId,
                Email = email,
                Name = "Test User",
                LwCoins = 50,
                FractionalLwCoins = 50.0,
                Level = 1,
                Experience = 0,
                ReferralCode = "TESTCODE"  
            };

            // 🔥 ИСПРАВЛЕНИЕ: Убираем ReferralCode из UserDto
            var userDto = new UserDto
            {
                Id = user.Id,
                Email = user.Email,
                Name = user.Name,
                LwCoins = user.LwCoins,
                Level = user.Level,
                Experience = user.Experience,
                // ReferralCode = user.ReferralCode,  // ❌ УДАЛИТЬ ЭТУ СТРОКУ
                MaxExperience = 100,
                ExperienceToNextLevel = 100,
                ExperienceProgress = 0
            };

            _userRepositoryMock.Setup(x => x.GetByEmailAsync(email))
                .ReturnsAsync((User?)null);

            _userRepositoryMock.Setup(x => x.CreateAsync(It.IsAny<User>()))
                .ReturnsAsync(user);

            _userRepositoryMock.Setup(x => x.GetByIdAsync(userId))
                .ReturnsAsync(user);

            _userRepositoryMock.Setup(x => x.GetByReferralCodeAsync(It.IsAny<string>()))
                .ReturnsAsync((User?)null);

            _lwCoinServiceMock.Setup(x => x.AddRegistrationBonusAsync(It.IsAny<string>()))
                .ReturnsAsync(true);

            _lwCoinServiceMock.Setup(x => x.GetUserLwCoinTransactionsAsync(It.IsAny<string>()))
                .ReturnsAsync(new List<LwCoinTransactionDto>());

            _emailServiceMock.Setup(x => x.SendVerificationEmailAsync(email, It.IsAny<string>()))
                .ReturnsAsync(true);

            // Act
            await _authService.SendVerificationCodeAsync(email);
            var result = await _authService.ConfirmEmailAsync(email, code);

            // Assert
            result.Should().NotBeNull();
            result.AccessToken.Should().NotBeNullOrEmpty();
            result.User.Should().NotBeNull();
            result.User.Email.Should().Be(email);
            result.User.LwCoins.Should().Be(50);

            _userRepositoryMock.Verify(x => x.CreateAsync(It.IsAny<User>()), Times.Once);
            _lwCoinServiceMock.Verify(x => x.AddRegistrationBonusAsync(It.IsAny<string>()), Times.Once);
        }

        [Fact]
        public async Task ConfirmEmailAsync_WithInvalidCode_ThrowsException()
        {
            var email = "test@example.com";
            var wrongCode = "999999";

            await Assert.ThrowsAsync<UnauthorizedAccessException>(
                async () => await _authService.ConfirmEmailAsync(email, wrongCode)
            );
        }
    }
}
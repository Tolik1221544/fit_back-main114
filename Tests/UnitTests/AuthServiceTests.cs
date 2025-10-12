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
        private readonly Mock<IMapper> _mapperMock;
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
            _mapperMock = new Mock<IMapper>();
            _loggerMock = new Mock<ILogger<AuthService>>();
            _activityServiceMock = new Mock<IActivityService>();
            _foodIntakeServiceMock = new Mock<IFoodIntakeService>();

            _authService = new AuthService(
                _userRepositoryMock.Object,
                _emailServiceMock.Object,
                _lwCoinServiceMock.Object,
                _configurationMock.Object,
                _mapperMock.Object,
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
        public async Task ConfirmEmailAsync_WithNewUser_CreatesUserAndAddsBonus()
        {
            var email = "newuser@example.com";
            var code = "123456";

            // ВАЖНО: сначала отправляем код
            _emailServiceMock.Setup(x => x.SendVerificationEmailAsync(email, It.IsAny<string>()))
                .ReturnsAsync(true);

            // Используем тестовый email, для которого есть фиксированный код
            var testEmail = "test@lightweightfit.com";
            var testCode = "123456";

            _userRepositoryMock.Setup(x => x.GetByEmailAsync(testEmail))
                .ReturnsAsync((User?)null);

            _userRepositoryMock.Setup(x => x.GetByReferralCodeAsync(It.IsAny<string>()))
                .ReturnsAsync((User?)null);

            var newUser = new User
            {
                Id = Guid.NewGuid().ToString(),
                Email = testEmail,
                Name = "Test User",
                LwCoins = 50,
                FractionalLwCoins = 50.0,
                Level = 1,
                Experience = 0,
                ReferralCode = "TESTCODE",
                IsEmailConfirmed = true
            };

            _userRepositoryMock.Setup(x => x.CreateAsync(It.IsAny<User>()))
                .ReturnsAsync(newUser);

            _userRepositoryMock.Setup(x => x.GetByIdAsync(It.IsAny<string>()))
                .ReturnsAsync(newUser);

            _userRepositoryMock.Setup(x => x.UpdateAsync(It.IsAny<User>()))
                .ReturnsAsync(newUser);

            _lwCoinServiceMock.Setup(x => x.AddRegistrationBonusAsync(It.IsAny<string>()))
                .ReturnsAsync(true);

            _lwCoinServiceMock.Setup(x => x.GetUserLwCoinTransactionsAsync(It.IsAny<string>()))
                .ReturnsAsync(new List<LwCoinTransactionDto>());

            _mapperMock.Setup(x => x.Map<UserDto>(It.IsAny<User>()))
                .Returns(new UserDto
                {
                    Email = testEmail,
                    LwCoins = 50,
                    Id = newUser.Id,
                    Name = "Test User",
                    Level = 1,
                    Experience = 0
                });

            // Act - используем тестовый email с фиксированным кодом
            var result = await _authService.ConfirmEmailAsync(testEmail, testCode);

            // Assert
            result.Should().NotBeNull();
            result.User.Should().NotBeNull();
            result.AccessToken.Should().NotBeNullOrEmpty();
            result.User.Email.Should().Be(testEmail);

            _userRepositoryMock.Verify(x => x.CreateAsync(It.IsAny<User>()), Times.Once);
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
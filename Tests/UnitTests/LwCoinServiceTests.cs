using Xunit;
using Moq;
using FluentAssertions;
using FitnessTracker.API.Services;
using FitnessTracker.API.Repositories;
using FitnessTracker.API.Models;
using FitnessTracker.API.DTOs;
using AutoMapper;
using Microsoft.Extensions.Logging;

namespace FitnessTracker.API.Tests.UnitTests
{
    public class LwCoinServiceTests
    {
        private readonly Mock<ILwCoinRepository> _lwCoinRepositoryMock;
        private readonly Mock<IUserRepository> _userRepositoryMock;
        private readonly Mock<IMapper> _mapperMock;
        private readonly Mock<ILogger<LwCoinService>> _loggerMock;
        private readonly LwCoinService _lwCoinService;

        public LwCoinServiceTests()
        {
            _lwCoinRepositoryMock = new Mock<ILwCoinRepository>();
            _userRepositoryMock = new Mock<IUserRepository>();
            _mapperMock = new Mock<IMapper>();
            _loggerMock = new Mock<ILogger<LwCoinService>>();

            _lwCoinService = new LwCoinService(
                _lwCoinRepositoryMock.Object,
                _userRepositoryMock.Object,
                _mapperMock.Object,
                _loggerMock.Object
            );
        }

        [Fact]
        public async Task AddRegistrationBonusAsync_ForNewUser_AddsCoinsSuccessfully()
        {
            // Arrange
            var userId = Guid.NewGuid().ToString();
            var user = new User
            {
                Id = userId,
                Email = "test@example.com",
                LwCoins = 0,
                FractionalLwCoins = 0.0
            };

            _userRepositoryMock.Setup(x => x.GetByIdAsync(userId))
                .ReturnsAsync(user);

            _lwCoinRepositoryMock.Setup(x => x.GetUserTransactionsAsync(userId))
                .ReturnsAsync(new List<LwCoinTransaction>());

            _userRepositoryMock.Setup(x => x.UpdateAsync(It.IsAny<User>()))
                .ReturnsAsync(user);

            _lwCoinRepositoryMock.Setup(x => x.CreateTransactionAsync(It.IsAny<LwCoinTransaction>()))
                .ReturnsAsync(new LwCoinTransaction());

            // Act
            var result = await _lwCoinService.AddRegistrationBonusAsync(userId);

            // Assert
            result.Should().BeTrue();
            _userRepositoryMock.Verify(x => x.UpdateAsync(It.Is<User>(u =>
                u.FractionalLwCoins == 50.0 && u.LwCoins == 50
            )), Times.Once);
        }

        [Fact]
        public async Task SpendLwCoinsAsync_WithSufficientBalance_DeductsCoinsSuccessfully()
        {
            // Arrange
            var userId = Guid.NewGuid().ToString();
            var user = new User
            {
                Id = userId,
                LwCoins = 100,
                FractionalLwCoins = 100.0
            };

            _userRepositoryMock.Setup(x => x.GetByIdAsync(userId))
                .ReturnsAsync(user);

            _lwCoinRepositoryMock.Setup(x => x.GetUserTransactionsAsync(userId))
                .ReturnsAsync(new List<LwCoinTransaction>
                {
                    new LwCoinTransaction
                    {
                        Amount = 100,
                        FractionalAmount = 100.0,
                        CoinSource = "permanent",
                        Type = "registration"
                    }
                });

            _lwCoinRepositoryMock.Setup(x => x.GetUserSubscriptionsAsync(userId))
                .ReturnsAsync(new List<Subscription>());

            _lwCoinRepositoryMock.Setup(x => x.CreateTransactionAsync(It.IsAny<LwCoinTransaction>()))
                .ReturnsAsync(new LwCoinTransaction());

            _userRepositoryMock.Setup(x => x.UpdateAsync(It.IsAny<User>()))
                .ReturnsAsync(user);

            // Act
            var result = await _lwCoinService.SpendLwCoinsAsync(
                userId, 1, "spent", "Test purchase", "photo"
            );

            // Assert
            result.Should().BeTrue();
            _userRepositoryMock.Verify(x => x.UpdateAsync(It.Is<User>(u =>
                u.FractionalLwCoins == 99.0
            )), Times.Once);
        }

        [Fact]
        public async Task SpendLwCoinsAsync_WithInsufficientBalance_ReturnsFalse()
        {
            // Arrange
            var userId = Guid.NewGuid().ToString();
            var user = new User
            {
                Id = userId,
                LwCoins = 0,
                FractionalLwCoins = 0.0
            };

            _userRepositoryMock.Setup(x => x.GetByIdAsync(userId))
                .ReturnsAsync(user);

            _lwCoinRepositoryMock.Setup(x => x.GetUserSubscriptionsAsync(userId))
                .ReturnsAsync(new List<Subscription>());

            // Act
            var result = await _lwCoinService.SpendLwCoinsAsync(
                userId, 1, "spent", "Test purchase", "photo"
            );

            // Assert
            result.Should().BeFalse();
        }

        [Fact]
        public async Task PurchaseSubscriptionCoinsAsync_CreatesSubscriptionAndAddsCoins()
        {
            // Arrange
            var userId = Guid.NewGuid().ToString();
            var user = new User
            {
                Id = userId,
                LwCoins = 0,
                FractionalLwCoins = 0.0
            };

            _userRepositoryMock.Setup(x => x.GetByIdAsync(userId))
                .ReturnsAsync(user);

            _userRepositoryMock.Setup(x => x.UpdateAsync(It.IsAny<User>()))
                .ReturnsAsync(user);

            _lwCoinRepositoryMock.Setup(x => x.CreateSubscriptionAsync(It.IsAny<Subscription>()))
                .ReturnsAsync(new Subscription { Id = Guid.NewGuid().ToString() });

            _lwCoinRepositoryMock.Setup(x => x.CreateTransactionAsync(It.IsAny<LwCoinTransaction>()))
                .ReturnsAsync(new LwCoinTransaction());

            // Act
            var result = await _lwCoinService.PurchaseSubscriptionCoinsAsync(
                userId, 100, 30, 2.99m
            );

            // Assert
            result.Should().BeTrue();
            _userRepositoryMock.Verify(x => x.UpdateAsync(It.Is<User>(u =>
                u.FractionalLwCoins == 100.0 && u.LwCoins == 100
            )), Times.Once);
            _lwCoinRepositoryMock.Verify(x => x.CreateSubscriptionAsync(It.IsAny<Subscription>()), Times.Once);
        }
    }
}
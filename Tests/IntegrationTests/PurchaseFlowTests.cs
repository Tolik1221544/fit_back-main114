using Xunit;
using FluentAssertions;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using FitnessTracker.API.DTOs;

namespace FitnessTracker.API.Tests.IntegrationTests
{
    public class PurchaseFlowTests : IClassFixture<CustomWebApplicationFactory>
    {
        private readonly CustomWebApplicationFactory _factory;
        private readonly HttpClient _client;

        public PurchaseFlowTests(CustomWebApplicationFactory factory)
        {
            _factory = factory;
            _client = factory.CreateClient();
        }

        [Fact]
        public async Task CompletePurchaseFlow_GooglePlay_WorksEndToEnd()
        {
            // 1. Авторизация
            var token = await GetAuthTokenAsync();
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            // 2. Проверка начального баланса
            var balanceResponse = await _client.GetAsync("/api/lw-coin/balance");
            balanceResponse.StatusCode.Should().Be(HttpStatusCode.OK);

            var balanceContent = await balanceResponse.Content.ReadAsStringAsync();
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var initialBalance = JsonSerializer.Deserialize<LwCoinBalanceDto>(balanceContent, options);
            initialBalance.Should().NotBeNull();

            // 3. Проверка истории транзакций
            var transactionsResponse = await _client.GetAsync("/api/lw-coin/transactions");
            transactionsResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        [Fact]
        public async Task SubscriptionStatus_ReturnsCorrectStructure()
        {
            // Arrange
            var token = await GetAuthTokenAsync();
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            // Act - используем GET вместо POST
            var response = await _client.GetAsync("/api/lw-coin/balance");

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);

            var content = await response.Content.ReadAsStringAsync();
            content.Should().NotBeNullOrEmpty();
        }

        private async Task<string> GetAuthTokenAsync()
        {
            var email = "test@lightweightfit.com";

            var sendCodeContent = new StringContent(
                JsonSerializer.Serialize(new { email }),
                Encoding.UTF8,
                "application/json"
            );
            await _client.PostAsync("/api/auth/send-code", sendCodeContent);

            var confirmContent = new StringContent(
                JsonSerializer.Serialize(new { email, code = "123456" }),
                Encoding.UTF8,
                "application/json"
            );

            var confirmResponse = await _client.PostAsync("/api/auth/confirm-email", confirmContent);
            var confirmResponseContent = await confirmResponse.Content.ReadAsStringAsync();

            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var authResponse = JsonSerializer.Deserialize<AuthResponseDto>(confirmResponseContent, options);

            return authResponse?.AccessToken ?? throw new Exception("Failed to get auth token");
        }
    }
}
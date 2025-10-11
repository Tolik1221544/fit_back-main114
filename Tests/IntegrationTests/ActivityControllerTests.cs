using Xunit;
using FluentAssertions;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using FitnessTracker.API.DTOs;

namespace FitnessTracker.API.Tests.IntegrationTests
{
    public class ActivityControllerTests : IClassFixture<CustomWebApplicationFactory>
    {
        private readonly CustomWebApplicationFactory _factory;
        private readonly HttpClient _client;

        public ActivityControllerTests(CustomWebApplicationFactory factory)
        {
            _factory = factory;
            _client = factory.CreateClient();
        }

        [Fact]
        public async Task GetActivities_WithoutAuth_ReturnsUnauthorized()
        {
            // Act
            var response = await _client.GetAsync("/api/activity");

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        }

        [Fact]
        public async Task AddActivity_WithValidData_ReturnsSuccess()
        {
            // Arrange
            var token = await GetAuthTokenAsync();
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var activity = new AddActivityRequest
            {
                Type = "cardio",
                StartDate = DateTime.UtcNow,
                EndDate = DateTime.UtcNow.AddMinutes(30),
                Calories = 300,
                ActivityData = new ActivityDataDto
                {
                    Name = "Test Run",
                    Category = "Cardio",
                    Distance = 5.0m
                }
            };

            var content = new StringContent(
                JsonSerializer.Serialize(activity),
                Encoding.UTF8,
                "application/json"
            );

            // Act
            var response = await _client.PostAsync("/api/activity", content);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);

            var responseContent = await response.Content.ReadAsStringAsync();
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var result = JsonSerializer.Deserialize<ActivityDto>(responseContent, options);

            result.Should().NotBeNull();
            result!.Type.Should().Be("cardio");
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
using Xunit;
using FluentAssertions;
using System.Net;

namespace FitnessTracker.API.Tests.IntegrationTests
{
    public class HealthCheckTests : IClassFixture<CustomWebApplicationFactory>
    {
        private readonly HttpClient _client;

        public HealthCheckTests(CustomWebApplicationFactory factory)
        {
            _client = factory.CreateClient();
        }

        [Fact]
        public async Task HealthCheck_ReturnsHealthy()
        {
            // Act
            var response = await _client.GetAsync("/api/health");

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);

            var content = await response.Content.ReadAsStringAsync();
            content.Should().Contain("healthy");
        }

        [Fact]
        public async Task DatabaseInfo_ReturnsCorrectStructure()
        {
            // Act
            var response = await _client.GetAsync("/api/health/db-info");

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);

            var content = await response.Content.ReadAsStringAsync();
            content.Should().Contain("users");
            content.Should().Contain("activities");
        }
    }
}
using System.Net;
using FluentAssertions;
using Turniejo.IntegrationTests.Infrastructure;

namespace Turniejo.IntegrationTests;

public sealed class SmokeTests(IntegrationTestWebApplicationFactory factory) : IntegrationTestBase(factory)
{
    [Fact]
    public async Task HealthCheck_ShouldReturnHealthy()
    {
        var client = CreateClient();

        var response = await client.GetAsync("/health");

        response.IsSuccessStatusCode.Should().BeTrue();
    }

    [Fact]
    public async Task Me_ShouldReturnUnauthorized_WhenNoTestAuthHeaderPresent()
    {
        var client = CreateClient();

        var response = await client.GetAsync("/api/me");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Me_ShouldReturnAuthenticatedUser_WhenTestAuthHeaderPresent()
    {
        var userId = Guid.NewGuid();
        var client = CreateAuthenticatedClient(userId, "smoke@integration.test");

        var response = await client.GetAsync("/api/me");

        response.IsSuccessStatusCode.Should().BeTrue();
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain(userId.ToString());
        body.Should().Contain("smoke@integration.test");
    }
}

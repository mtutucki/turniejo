using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Turniejo.IntegrationTests.Infrastructure;

namespace Turniejo.IntegrationTests.Scenarios;

// Motyw aplikacji na koncie (PUT /api/me/theme, pole w /api/me/profile), żeby szedł za userem między urządzeniami.
public sealed class ThemeTests(IntegrationTestWebApplicationFactory factory) : IntegrationTestBase(factory)
{
    private static async Task<string?> ThemeAsync(HttpClient client) =>
        (await (await client.GetAsync("/api/me/profile")).ReadJsonAsync()).GetProperty("theme").GetString();

    [Fact]
    public async Task NewAccount_FollowsTheSystemTheme()
    {
        var client = await TournamentFlow.RegisterAndAuthenticateAsync(Factory, "theme-new@integration.test");

        (await ThemeAsync(client)).Should().Be("system");
    }

    [Theory]
    [InlineData("light")]
    [InlineData("dark")]
    [InlineData("system")]
    public async Task Theme_IsSavedOnTheAccount(string theme)
    {
        var client = await TournamentFlow.RegisterAndAuthenticateAsync(Factory, $"theme-{theme}@integration.test");

        (await client.PutAsJsonAsync("/api/me/theme", new { theme })).StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await ThemeAsync(client)).Should().Be(theme);
    }

    [Theory]
    [InlineData("blue")]
    [InlineData("Light")]
    [InlineData("1")]
    public async Task Theme_RejectsUnknownValues(string theme)
    {
        var client = await TournamentFlow.RegisterAndAuthenticateAsync(Factory, $"theme-bad-{theme}@integration.test");

        var response = await client.PutAsJsonAsync("/api/me/theme", new { theme });

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await ThemeAsync(client)).Should().Be("system");
    }

    [Fact]
    // Jak w PermissionMatrixTests: kontrolery odrzucają anonima przez UnauthorizedException (403).
    public async Task Theme_RequiresLogin()
    {
        var response = await CreateClient().PutAsJsonAsync("/api/me/theme", new { theme = "light" });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}

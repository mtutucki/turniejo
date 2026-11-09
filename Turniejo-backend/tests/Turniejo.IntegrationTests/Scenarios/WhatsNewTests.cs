using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Turniejo.IntegrationTests.Infrastructure;

namespace Turniejo.IntegrationTests.Scenarios;

// Okno "Co nowego": ostatnia obejrzana wersja na koncie (PUT /api/me/seen-release, pole w /api/me/profile).
public sealed class WhatsNewTests(IntegrationTestWebApplicationFactory factory) : IntegrationTestBase(factory)
{
    private static async Task<string?> SeenAsync(HttpClient client) =>
        (await (await client.GetAsync("/api/me/profile")).ReadJsonAsync()).GetProperty("seenReleaseVersion").GetString();

    [Fact]
    public async Task NewAccount_HasNotSeenAnyRelease()
    {
        var client = await TournamentFlow.RegisterAndAuthenticateAsync(Factory, "whatsnew-new@integration.test");

        (await SeenAsync(client)).Should().BeNull();
    }

    [Fact]
    public async Task SeenRelease_IsSavedOnTheAccount_AndNeverGoesBack()
    {
        var client = await TournamentFlow.RegisterAndAuthenticateAsync(Factory, "whatsnew-seen@integration.test");

        (await client.PutAsJsonAsync("/api/me/seen-release", new { version = "1.2.0" })).StatusCode.Should().Be(HttpStatusCode.NoContent);
        // Karta otwarta przed wdrożeniem wysyła starszą wersję: zapis zostaje.
        (await client.PutAsJsonAsync("/api/me/seen-release", new { version = "1.1.0" })).StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await SeenAsync(client)).Should().Be("1.2.0");
    }

    [Fact]
    public async Task SeenRelease_RejectsABuildVersion()
    {
        var client = await TournamentFlow.RegisterAndAuthenticateAsync(Factory, "whatsnew-bad@integration.test");

        var response = await client.PutAsJsonAsync("/api/me/seen-release", new { version = "1.2.0-3-gabc1234" });

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await SeenAsync(client)).Should().BeNull();
    }

    [Fact]
    // Jak w PermissionMatrixTests: kontrolery odrzucają anonima przez UnauthorizedException (403).
    public async Task SeenRelease_RequiresLogin()
    {
        var response = await CreateClient().PutAsJsonAsync("/api/me/seen-release", new { version = "1.2.0" });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}

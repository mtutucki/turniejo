using System.Net.Http.Json;
using FluentAssertions;
using Turniejo.IntegrationTests.Infrastructure;
using Turniejo.Users.Application.Users.RegisterUser;

namespace Turniejo.IntegrationTests.Scenarios;

// Wszystkie pozostałe scenariusze zakładają aktorów przez bezpośredni zapis EF (patrz
// TournamentFlow.RegisterAndAuthenticateAsync), żeby nie potrącać o rate limiter AuthAnonymous
// współdzielony na cały przebieg testów. Ten jeden test celowo idzie prawdziwą ścieżką HTTP, żeby
// realny flow rejestracji (FakeKeycloakAdminService -> zapis do Users) też miał pokrycie E2E.
public sealed class RegistrationHttpFlowTests(IntegrationTestWebApplicationFactory factory) : IntegrationTestBase(factory)
{
    [Fact]
    public async Task Register_CreatesUser_AndAllowsAuthenticatedMeCallWithReturnedId()
    {
        var anonymous = CreateClient();

        var registerResponse = await anonymous.PostAsJsonAsync("/api/auth/register",
            new RegisterUserCommand("http-flow@integration.test", "Sup3rSecret!", "Http", "Flow"));
        registerResponse.IsSuccessStatusCode.Should().BeTrue();

        var userId = (await registerResponse.ReadJsonAsync()).GetGuid("userId");

        var authenticated = CreateAuthenticatedClient(userId, "http-flow@integration.test");
        var meResponse = await authenticated.GetAsync("/api/me");
        meResponse.IsSuccessStatusCode.Should().BeTrue();

        var me = await meResponse.ReadJsonAsync();
        me.GetGuid("userId").Should().Be(userId);
    }

    [Fact]
    public async Task Register_Fails_WhenEmailAlreadyRegistered()
    {
        var anonymous = CreateClient();
        var command = new RegisterUserCommand("duplicate@integration.test", "Sup3rSecret!", "First", "Attempt");

        (await anonymous.PostAsJsonAsync("/api/auth/register", command)).IsSuccessStatusCode.Should().BeTrue();

        var secondAttempt = await anonymous.PostAsJsonAsync("/api/auth/register", command with { FirstName = "Second" });

        secondAttempt.IsSuccessStatusCode.Should().BeFalse();
    }
}

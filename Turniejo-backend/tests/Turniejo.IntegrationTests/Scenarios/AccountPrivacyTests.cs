using System.Net;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Turniejo.IntegrationTests.Infrastructure;
using Turniejo.Users.Application.Abstractions;
using Turniejo.Users.Domain.Enums;

namespace Turniejo.IntegrationTests.Scenarios;

// Prawa z RODO: eksport własnych danych (GET /api/me/export) i usunięcie konta (DELETE /api/me).
public sealed class AccountPrivacyTests(IntegrationTestWebApplicationFactory factory) : IntegrationTestBase(factory)
{
    [Fact]
    public async Task Export_ReturnsOwnProfile()
    {
        var client = await TournamentFlow.RegisterAndAuthenticateAsync(Factory, "export@integration.test");

        var response = await client.GetAsync("/api/me/export");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var export = await response.ReadJsonAsync();
        export.GetProperty("profile").GetProperty("email").GetString().Should().Be("export@integration.test");
    }

    [Fact]
    public async Task Delete_AnonymizesUser_RemovesRoles_AndDeletesKeycloakAccount()
    {
        var userId = await TournamentFlow.SeedUserAsync(Factory, "delete-me@integration.test");
        var client = CreateAuthenticatedClient(userId, "delete-me@integration.test");

        var response = await client.DeleteAsync("/api/me");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        Factory.FakeKeycloak.DeletedUsers.Should().Contain(userId);

        using var scope = Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IUsersDbContext>();
        var user = await dbContext.GetUserByIdAsync(userId);
        user!.Status.Should().Be(UserStatus.Deleted);
        user.Email.Should().NotContain("delete-me");
        user.FirstName.Should().Be("Usunięty");
        (await dbContext.GetUserRolesAsync(userId)).Should().BeEmpty();
    }

    [Fact]
    public async Task Delete_IsRejected_ForSoleOrganizerOfOrganization()
    {
        var userId = await TournamentFlow.SeedUserAsync(Factory, "owner@integration.test");
        var client = CreateAuthenticatedClient(userId, "owner@integration.test");
        await TournamentFlow.CreateOrganizationAsync(client, "Klub Testowy");

        var response = await client.DeleteAsync("/api/me");

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        Factory.FakeKeycloak.DeletedUsers.Should().NotContain(userId);
    }
}

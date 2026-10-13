using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Turniejo.Api.Controllers;
using Turniejo.IntegrationTests.Infrastructure;
using Turniejo.Organizations.Application.Abstractions;
using Turniejo.Players.Application.Abstractions;
using Turniejo.Players.Domain.Enums;
using Turniejo.Teams.Application.Abstractions;
using Turniejo.Tournaments.Application.Abstractions;
using Turniejo.Tournaments.Application.Tournaments.CreateTournament;
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

    [Fact]
    public async Task Delete_WithConfirmation_RemovesSoleOrganizersOrganizationWithItsData()
    {
        var userId = await TournamentFlow.SeedUserAsync(Factory, "owner@integration.test");
        var client = CreateAuthenticatedClient(userId, "owner@integration.test");
        var organizationId = await TournamentFlow.CreateOrganizationAsync(client, "Klub Testowy");
        var teamId = await TournamentFlow.CreateTeamAsync(client, organizationId, "Orły");
        var now = DateTimeOffset.UtcNow;
        var created = await client.PostAsJsonAsync("/api/tournaments",
            new CreateTournamentCommand(organizationId, "Wiosna", now, now.AddDays(2), 16, null));
        var tournamentId = (await created.ReadJsonAsync()).GetGuid("tournamentId");
        var categoryId = await TournamentFlow.AddCategoryAsync(client, tournamentId, "Open");
        var tournamentTeamId = await TournamentFlow.AddTeamToTournamentAsync(client, tournamentId, teamId, categoryId);
        var playerResponse = await client.PostAsJsonAsync($"/api/tournaments/{tournamentId}/tournament-teams/{tournamentTeamId}/players/new",
            new TournamentsController.CreatePlayerInTournamentTeamRequest(
                "Jan", "Kowalski", new DateTime(2012, 5, 1, 0, 0, 0, DateTimeKind.Utc), PlayerPosition.Forward, null, 9, PlayerPosition.Forward));
        var playerId = (await playerResponse.ReadJsonAsync()).GetGuid("playerId");
        var refereeId = await TournamentFlow.SeedUserAsync(Factory, "referee@integration.test");
        await TournamentFlow.JoinAsRefereeAsync(Factory, client, tournamentId, refereeId);

        var response = await client.DeleteAsync("/api/me?deleteOrganization=true");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        Factory.FakeKeycloak.DeletedUsers.Should().Contain(userId);

        using var scope = Factory.Services.CreateScope();
        var services = scope.ServiceProvider;
        var users = services.GetRequiredService<IUsersDbContext>();
        (await users.GetUserByIdAsync(userId))!.Status.Should().Be(UserStatus.Deleted);
        (await users.GetUserRolesAsync(userId)).Should().BeEmpty();
        // Sędzia zachowuje konto, ale traci rolę w turnieju, którego już nie ma.
        (await users.GetUserByIdAsync(refereeId))!.Status.Should().Be(UserStatus.Active);
        (await users.GetUserRolesAsync(refereeId)).Should().BeEmpty();
        (await services.GetRequiredService<IOrganizationsDbContext>().GetOrganizationByOwnerUserIdAsync(userId)).Should().BeNull();
        (await services.GetRequiredService<ITournamentsDbContext>().GetTournamentByIdAsync(tournamentId)).Should().BeNull();
        var teams = services.GetRequiredService<ITeamsDbContext>();
        (await teams.GetTeamByIdAsync(teamId)).Should().BeNull();
        (await teams.GetTournamentTeamByIdAsync(tournamentTeamId)).Should().BeNull();
        (await services.GetRequiredService<IPlayersDbContext>().GetPlayerByIdAsync(playerId)).Should().BeNull();
    }
}

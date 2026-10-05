using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Turniejo.Api.Controllers;
using Turniejo.IntegrationTests.Infrastructure;
using Turniejo.Tournaments.Application.Tournaments.CreateTournament;
using Turniejo.Tournaments.Domain.Enums;

namespace Turniejo.IntegrationTests.Scenarios;

// Spec #36: "uprawnienia ról (403 dla cudzych zasobów)" — cała autoryzacja w tej aplikacji
// idzie przez AuthorizationBehavior<,> w pipeline MediatR, nie przez [Authorize] na kontrolerach;
// ExceptionHandlingMiddleware mapuje UnauthorizedException (brak uwierzytelnienia LUB brak roli)
// na 403 w obu przypadkach — nie ma tu ścieżki 401.
public sealed class AuthorizationTests(IntegrationTestWebApplicationFactory factory) : IntegrationTestBase(factory)
{
    [Fact]
    public async Task ModifyingSomeoneElsesTournament_Returns403_ForAuthenticatedNonOwner()
    {
        var owner = await TournamentFlow.RegisterAndAuthenticateAsync(Factory, "owner@integration.test");
        var intruder = await TournamentFlow.RegisterAndAuthenticateAsync(Factory, "intruder@integration.test");

        var organizationId = await TournamentFlow.CreateOrganizationAsync(owner, "Owner's Org");

        var now = DateTimeOffset.UtcNow;
        var createTournament = new CreateTournamentCommand(organizationId, "Owner's Cup", now, now.AddDays(2), 16, null);
        var tournamentResponse = await owner.PostAsJsonAsync("/api/tournaments", createTournament);
        tournamentResponse.IsSuccessStatusCode.Should().BeTrue();
        var tournamentId = (await tournamentResponse.ReadJsonAsync()).GetGuid("tournamentId");

        // Intruder is authenticated (real, registered user) but has no UserRoleAssignment for this
        // tournament or its organization — every mutation on it must be rejected.
        var addCategoryAsIntruder = await intruder.PostAsJsonAsync($"/api/tournaments/{tournamentId}/categories",
            new TournamentsController.AddTournamentCategoryRequest("Hijacked Category", null, null, 1));
        addCategoryAsIntruder.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var changeStatusAsIntruder = await intruder.PatchAsJsonAsync($"/api/tournaments/{tournamentId}/status",
            new TournamentsController.ChangeTournamentStatusRequest(TournamentStatus.RegistrationOpen));
        changeStatusAsIntruder.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var createTeamAsIntruder = await intruder.PostAsJsonAsync($"/api/organizations/{organizationId}/teams",
            new TeamsController.CreateTeamRequest("Hijacked Team", null, null));
        createTeamAsIntruder.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        // The legitimate owner, meanwhile, can do all of the above.
        var addCategoryAsOwner = await owner.PostAsJsonAsync($"/api/tournaments/{tournamentId}/categories",
            new TournamentsController.AddTournamentCategoryRequest("Legit Category", null, null, 1));
        addCategoryAsOwner.IsSuccessStatusCode.Should().BeTrue();
    }

    [Fact]
    public async Task ModifyingATournament_Returns403_WhenNotAuthenticated()
    {
        var owner = await TournamentFlow.RegisterAndAuthenticateAsync(Factory, "owner2@integration.test");
        var organizationId = await TournamentFlow.CreateOrganizationAsync(owner, "Owner's Org 2");

        var now = DateTimeOffset.UtcNow;
        var tournamentResponse = await owner.PostAsJsonAsync("/api/tournaments",
            new CreateTournamentCommand(organizationId, "Owner's Cup 2", now, now.AddDays(2), 16, null));
        tournamentResponse.IsSuccessStatusCode.Should().BeTrue();
        var tournamentId = (await tournamentResponse.ReadJsonAsync()).GetGuid("tournamentId");

        var anonymous = CreateClient();
        var response = await anonymous.PostAsJsonAsync($"/api/tournaments/{tournamentId}/categories",
            new TournamentsController.AddTournamentCategoryRequest("Anonymous Category", null, null, 1));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}

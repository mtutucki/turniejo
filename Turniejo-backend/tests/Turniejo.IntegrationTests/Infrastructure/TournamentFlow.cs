using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Turniejo.Api.Controllers;
using Turniejo.Matches.Domain.Enums;
using Turniejo.Teams.Domain.Enums;
using Turniejo.Tournaments.Domain.Enums;
using Turniejo.Users.Application.Abstractions;
using Turniejo.Users.Domain.Entities;

namespace Turniejo.IntegrationTests.Infrastructure;

// Ciąg HTTP-calli powtarzalny w kilku scenariuszach (grupowy E2E, drabinka, autoryzacja) —
// wyciągnięty raz, żeby każdy test opisywał TYLKO to, co go faktycznie odróżnia.
public static class TournamentFlow
{
    // CreateOrganization/CreateTournament itd. wpisują UserRoleAssignment z FK na users.users —
    // TestAuthHandler sam nie tworzy tam wiersza (to tylko nagłówki HTTP), więc każdy "aktor"
    // scenariusza potrzebuje realnego wiersza w Users. Zapis bezpośrednio przez EF (nie przez
    // POST /api/auth/register) celowo: rejestracja podlega rate limiterowi AuthAnonymous (5/5min,
    // IP-keyed) współdzielonemu przez CAŁY przebieg testów (jeden host na całą kolekcję) — kilka
    // scenariuszy w jednym runie łatwo o to potrąca, a żaden z nich i tak nie testuje samej
    // rejestracji (to pokrywa dedykowany RegistrationHttpFlowTests).
    public static async Task<HttpClient> RegisterAndAuthenticateAsync(IntegrationTestWebApplicationFactory factory, string email)
    {
        var userId = await SeedUserAsync(factory, email);

        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.UserIdHeader, userId.ToString());
        client.DefaultRequestHeaders.Add(TestAuthHandler.EmailHeader, email);
        return client;
    }

    // Dla przypadków, gdzie test potrzebuje TYLKO realnego wiersza w Users (np. jako cel
    // AssignRefereeToMatchCommand, który sam wpisuje UserRoleAssignment z FK na ten wiersz), bez
    // klienta HTTP działającego w jego imieniu.
    public static async Task<Guid> SeedUserAsync(IntegrationTestWebApplicationFactory factory, string email)
    {
        var userId = Guid.NewGuid();
        var user = User.Create(userId, email, "Test", "User").Value;

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IUsersDbContext>();
        await dbContext.AddUserAsync(user, CancellationToken.None);
        await dbContext.SaveChangesAsync(CancellationToken.None);

        return userId;
    }

    // Sędzię można przypisać do meczu tylko, gdy ma rolę Referee w turnieju — tak jak w aplikacji
    // dostaje ją przez dołączenie kodem sędziego.
    public static async Task JoinAsRefereeAsync(IntegrationTestWebApplicationFactory factory, HttpClient organizer, Guid tournamentId, Guid refereeUserId)
    {
        var tournament = await (await organizer.GetAsync($"/api/tournaments/{tournamentId}")).ReadJsonAsync();
        var refereeCode = tournament.GetProperty("refereeCode").GetString();

        var referee = factory.CreateClient();
        referee.DefaultRequestHeaders.Add(TestAuthHandler.UserIdHeader, refereeUserId.ToString());
        referee.DefaultRequestHeaders.Add(TestAuthHandler.EmailHeader, $"{refereeUserId}@integration.test");

        var response = await referee.PostAsJsonAsync("/api/tournaments/join", new { code = refereeCode });
        response.IsSuccessStatusCode.Should().BeTrue();
    }

    public static async Task<Guid> CreateOrganizationAsync(HttpClient client, string name)
    {
        var response = await client.PostAsJsonAsync("/api/organizations", new CreateOrganizationRequest(name));
        response.IsSuccessStatusCode.Should().BeTrue();
        return (await response.ReadJsonAsync()).GetGuid("organizationId");
    }

    public static async Task<Guid> CreateTeamAsync(HttpClient client, Guid organizationId, string name)
    {
        var response = await client.PostAsJsonAsync($"/api/organizations/{organizationId}/teams",
            new TeamsController.CreateTeamRequest(name, null, null));
        response.IsSuccessStatusCode.Should().BeTrue();
        return (await response.ReadJsonAsync()).GetGuid("teamId");
    }

    public static async Task<Guid> AddTeamToTournamentAsync(HttpClient client, Guid tournamentId, Guid teamId, Guid categoryId)
    {
        var response = await client.PostAsJsonAsync($"/api/tournaments/{tournamentId}/teams",
            new TournamentsController.AddTeamToTournamentRequest(teamId, categoryId));
        response.IsSuccessStatusCode.Should().BeTrue();
        return (await response.ReadJsonAsync()).GetGuid("tournamentTeamId");
    }

    public static async Task AssignTeamToGroupAsync(HttpClient client, Guid tournamentId, Guid tournamentTeamId, Guid groupId)
    {
        var response = await client.PatchAsJsonAsync($"/api/tournaments/{tournamentId}/tournament-teams/{tournamentTeamId}/group",
            new TournamentsController.AssignTeamToGroupRequest(groupId));
        response.IsSuccessStatusCode.Should().BeTrue();
    }

    public static async Task AcceptRegistrationAsync(HttpClient client, Guid tournamentId, Guid tournamentTeamId)
    {
        var response = await client.PatchAsJsonAsync($"/api/tournaments/{tournamentId}/tournament-teams/{tournamentTeamId}/registration-status",
            new TournamentsController.ChangeTournamentTeamRegistrationStatusRequest(RegistrationStatus.Accepted));
        response.IsSuccessStatusCode.Should().BeTrue();
    }

    public static async Task ChangeStatusAsync(HttpClient client, Guid tournamentId, TournamentStatus status)
    {
        var response = await client.PatchAsJsonAsync($"/api/tournaments/{tournamentId}/status",
            new TournamentsController.ChangeTournamentStatusRequest(status));
        response.IsSuccessStatusCode.Should().BeTrue();
    }

    public static async Task<Guid> AddPitchAsync(HttpClient client, Guid tournamentId, string name)
    {
        var response = await client.PostAsJsonAsync($"/api/tournaments/{tournamentId}/pitches",
            new MatchesController.AddPitchRequest(name, null));
        response.IsSuccessStatusCode.Should().BeTrue();
        return (await response.ReadJsonAsync()).GetGuid("pitchId");
    }

    public static async Task AddGoalAsync(HttpClient client, Guid tournamentId, Guid matchId, Guid scoringTeamId, int minute)
    {
        var response = await client.PostAsJsonAsync($"/api/tournaments/{tournamentId}/matches/{matchId}/goals",
            new MatchesController.AddGoalRequest(scoringTeamId, null, null, MatchEventType.Goal, minute, null));
        response.IsSuccessStatusCode.Should().BeTrue();
    }

    public static async Task<Guid> AddCategoryAsync(HttpClient client, Guid tournamentId, string name)
    {
        var response = await client.PostAsJsonAsync($"/api/tournaments/{tournamentId}/categories",
            new TournamentsController.AddTournamentCategoryRequest(name, null, null, 1));
        response.IsSuccessStatusCode.Should().BeTrue();
        return (await response.ReadJsonAsync()).GetGuid("tournamentCategoryId");
    }
}

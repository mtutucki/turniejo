using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Turniejo.Api.Controllers;
using Turniejo.Brackets.Domain.Enums;
using Turniejo.IntegrationTests.Infrastructure;
using Turniejo.Matches.Domain.Enums;
using Turniejo.Tournaments.Application.Tournaments.CreateTournament;
using Turniejo.Tournaments.Domain.Enums;

namespace Turniejo.IntegrationTests.Scenarios;

// QA v3: GPK-006 - drużyna z miejsca w grupie, która ma wolny los, czekała na koniec całej fazy grupowej,
// zamiast przejść dalej, gdy tylko jej grupa się skończyła.
public sealed class GroupToBracketTests(IntegrationTestWebApplicationFactory factory) : IntegrationTestBase(factory)
{
    [Fact]
    public async Task GPK_006_ByeFromFinishedGroup_AdvancesWhileOtherGroupStillPlays()
    {
        var organizer = await TournamentFlow.RegisterAndAuthenticateAsync(Factory, "gpk006@integration.test");
        var now = DateTimeOffset.UtcNow;
        var organizationId = await TournamentFlow.CreateOrganizationAsync(organizer, "GPK006 Org");

        var tournamentResponse = await organizer.PostAsJsonAsync("/api/tournaments",
            new CreateTournamentCommand(organizationId, "GPK006 Cup", now, now.AddDays(2), 16, null));
        var tournamentId = (await tournamentResponse.ReadJsonAsync()).GetGuid("tournamentId");
        (await organizer.PatchAsJsonAsync($"/api/tournaments/{tournamentId}/format",
                new TournamentsController.UpdateTournamentFormatRequest(TournamentFormat.GroupAndKnockout)))
            .IsSuccessStatusCode.Should().BeTrue();

        var categoryId = await TournamentFlow.AddCategoryAsync(organizer, tournamentId, "Open");
        var groupA = await AddGroupAsync(organizer, tournamentId, categoryId, "A", 1);
        var groupB = await AddGroupAsync(organizer, tournamentId, categoryId, "B", 2);

        var a1 = await AddTeamAsync(organizer, organizationId, tournamentId, categoryId, groupA, "A1");
        var a2 = await AddTeamAsync(organizer, organizationId, tournamentId, categoryId, groupA, "A2");
        var b1 = await AddTeamAsync(organizer, organizationId, tournamentId, categoryId, groupB, "B1");
        var b2 = await AddTeamAsync(organizer, organizationId, tournamentId, categoryId, groupB, "B2");

        await TournamentFlow.ChangeStatusAsync(organizer, tournamentId, TournamentStatus.RegistrationOpen);
        await TournamentFlow.ChangeStatusAsync(organizer, tournamentId, TournamentStatus.RegistrationClosed);
        var pitchId = await TournamentFlow.AddPitchAsync(organizer, tournamentId, "Pitch 1");
        (await organizer.PostAsJsonAsync($"/api/tournaments/{tournamentId}/schedule/generate",
                new MatchesController.GenerateScheduleRequest(now, now.AddHours(6), [pitchId])))
            .IsSuccessStatusCode.Should().BeTrue();

        // Półfinały: 1. miejsce A - wolny los, 1. miejsce B - 2. miejsce A.
        (await organizer.PostAsJsonAsync($"/api/tournaments/{tournamentId}/bracket/generate",
                new BracketsController.GenerateBracketRequest(categoryId,
                [
                    new BracketsController.BracketPairingRequest(BracketSourceType.GroupPosition, groupA, 1, null, BracketSourceType.Bye, null, null, null),
                    new BracketsController.BracketPairingRequest(BracketSourceType.GroupPosition, groupB, 1, null, BracketSourceType.GroupPosition, groupA, 2, null),
                ])))
            .IsSuccessStatusCode.Should().BeTrue();

        // Gra tylko grupa A: A1 wygrywa 2:0. Grupa B ma dalej mecz niezakończony.
        var schedule = (await (await organizer.GetAsync($"/api/tournaments/{tournamentId}/schedule")).ReadJsonAsync()).EnumerateArray().ToList();
        var groupAMatch = schedule.Single(m => m.GetGuidOrNull("groupId") == groupA);
        var homeIsA1 = groupAMatch.GetGuid("homeTournamentTeamId") == a1;
        await PlayAsync(organizer, tournamentId, groupAMatch.GetGuid("id"), winner: a1, loser: a2, winnerIsHome: homeIsA1);
        await FlushOutboxAsync();

        var bracket = await (await organizer.GetAsync($"/api/tournaments/{tournamentId}/bracket?categoryId={categoryId}")).ReadJsonAsync();
        var final = bracket.GetProperty("rounds").EnumerateArray().Single(r => r.GetInt("roundNumber") == 2)
            .GetProperty("nodes").EnumerateArray().Single();

        var finalTeams = new[] { final.GetGuidOrNull("homeTournamentTeamId"), final.GetGuidOrNull("awayTournamentTeamId") };
        finalTeams.Should().Contain(a1, "drużyna z wolnym losem przechodzi dalej, gdy jej grupa jest rozegrana");
    }

    private static async Task<Guid> AddGroupAsync(HttpClient client, Guid tournamentId, Guid categoryId, string name, int order)
    {
        var response = await client.PostAsJsonAsync($"/api/tournaments/{tournamentId}/groups",
            new TournamentsController.AddTournamentGroupRequest(categoryId, name, order));
        response.IsSuccessStatusCode.Should().BeTrue();
        return (await response.ReadJsonAsync()).GetGuid("tournamentGroupId");
    }

    private static async Task<Guid> AddTeamAsync(HttpClient client, Guid organizationId, Guid tournamentId, Guid categoryId, Guid groupId, string name)
    {
        var teamId = await TournamentFlow.CreateTeamAsync(client, organizationId, name);
        var ttId = await TournamentFlow.AddTeamToTournamentAsync(client, tournamentId, teamId, categoryId);
        await TournamentFlow.AssignTeamToGroupAsync(client, tournamentId, ttId, groupId);
        await TournamentFlow.AcceptRegistrationAsync(client, tournamentId, ttId);
        return ttId;
    }

    private static async Task PlayAsync(HttpClient client, Guid tournamentId, Guid matchId, Guid winner, Guid loser, bool winnerIsHome)
    {
        (await client.PostAsync($"/api/tournaments/{tournamentId}/matches/{matchId}/start", null)).IsSuccessStatusCode.Should().BeTrue();
        await TournamentFlow.AddGoalAsync(client, tournamentId, matchId, winner, 10);
        await TournamentFlow.AddGoalAsync(client, tournamentId, matchId, winner, 20);
        (await client.PostAsJsonAsync($"/api/tournaments/{tournamentId}/matches/{matchId}/finish",
                new MatchesController.FinishMatchRequest(MatchResultType.Regular, winner)))
            .IsSuccessStatusCode.Should().BeTrue();
    }
}

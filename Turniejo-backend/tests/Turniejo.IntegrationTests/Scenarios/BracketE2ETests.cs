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

// Spec #36: "drabinka playoff + awans" — 4 drużyny, 2 mecze Rundy 1, zwycięzcy awansują
// automatycznie (przez outbox -> MatchFinished -> AdvanceBracketWinnerCommand) do Finału.
public sealed class BracketE2ETests(IntegrationTestWebApplicationFactory factory) : IntegrationTestBase(factory)
{
    [Fact]
    public async Task Bracket_RoundOneWinners_AdvanceAutomaticallyIntoFinal()
    {
        var organizer = await TournamentFlow.RegisterAndAuthenticateAsync(Factory, "bracket-organizer@integration.test");
        var now = DateTimeOffset.UtcNow;

        var organizationId = await TournamentFlow.CreateOrganizationAsync(organizer, "Bracket Org");

        var createTournament = new CreateTournamentCommand(organizationId, "Knockout Cup", now, now.AddDays(2), 16, null);
        var tournamentResponse = await organizer.PostAsJsonAsync("/api/tournaments", createTournament);
        tournamentResponse.IsSuccessStatusCode.Should().BeTrue();
        var tournamentId = (await tournamentResponse.ReadJsonAsync()).GetGuid("tournamentId");

        (await organizer.PatchAsJsonAsync($"/api/tournaments/{tournamentId}/format",
                new TournamentsController.UpdateTournamentFormatRequest(TournamentFormat.Knockout)))
            .IsSuccessStatusCode.Should().BeTrue();

        var categoryId = await TournamentFlow.AddCategoryAsync(organizer, tournamentId, "Open");

        var teamAId = await TournamentFlow.CreateTeamAsync(organizer, organizationId, "Team A");
        var teamBId = await TournamentFlow.CreateTeamAsync(organizer, organizationId, "Team B");
        var teamCId = await TournamentFlow.CreateTeamAsync(organizer, organizationId, "Team C");
        var teamDId = await TournamentFlow.CreateTeamAsync(organizer, organizationId, "Team D");

        var ttA = await TournamentFlow.AddTeamToTournamentAsync(organizer, tournamentId, teamAId, categoryId);
        var ttB = await TournamentFlow.AddTeamToTournamentAsync(organizer, tournamentId, teamBId, categoryId);
        var ttC = await TournamentFlow.AddTeamToTournamentAsync(organizer, tournamentId, teamCId, categoryId);
        var ttD = await TournamentFlow.AddTeamToTournamentAsync(organizer, tournamentId, teamDId, categoryId);

        var generateResponse = await organizer.PostAsJsonAsync($"/api/tournaments/{tournamentId}/bracket/generate",
            new BracketsController.GenerateBracketRequest(categoryId,
            [
                new BracketsController.BracketPairingRequest(BracketSourceType.Fixed, null, null, ttA, BracketSourceType.Fixed, null, null, ttB),
                new BracketsController.BracketPairingRequest(BracketSourceType.Fixed, null, null, ttC, BracketSourceType.Fixed, null, null, ttD)
            ]));
        generateResponse.IsSuccessStatusCode.Should().BeTrue();
        var generated = await generateResponse.ReadJsonAsync();
        generated.GetInt("roundCount").Should().Be(2);

        var bracketAfterGenerate = await GetBracketAsync(organizer, tournamentId, categoryId);
        var round1 = bracketAfterGenerate.GetProperty("rounds").EnumerateArray().Single(r => r.GetInt("roundNumber") == 1);
        var round1Nodes = round1.GetProperty("nodes").EnumerateArray().ToList();
        round1Nodes.Should().HaveCount(2);

        var nodeAB = round1Nodes.Single(n => n.GetGuidOrNull("homeTournamentTeamId") == ttA);
        var nodeCD = round1Nodes.Single(n => n.GetGuidOrNull("homeTournamentTeamId") == ttC);
        var matchAB = nodeAB.GetGuid("matchId");
        var matchCD = nodeCD.GetGuid("matchId");

        // A bije B 3:1, C bije D 2:0 — zwycięzcy to A i C.
        await PlayToFinishAsync(organizer, tournamentId, matchAB, ttA, ttB, homeGoals: 3, awayGoals: 1, winnerTournamentTeamId: ttA);
        await PlayToFinishAsync(organizer, tournamentId, matchCD, ttC, ttD, homeGoals: 2, awayGoals: 0, winnerTournamentTeamId: ttC);

        await FlushOutboxAsync();

        var bracketAfterRound1 = await GetBracketAsync(organizer, tournamentId, categoryId);
        var finalRound = bracketAfterRound1.GetProperty("rounds").EnumerateArray().Single(r => r.GetInt("roundNumber") == 2);
        var finalNode = finalRound.GetProperty("nodes").EnumerateArray().Single();

        finalRound.GetString("name").Should().Be("Final");
        finalNode.GetGuidOrNull("homeTournamentTeamId").Should().Be(ttA);
        finalNode.GetGuidOrNull("awayTournamentTeamId").Should().Be(ttC);
        // Węzeł w pełni rozwiązany -> mecz Finału powstaje automatycznie (bez osobnego wywołania).
        finalNode.TryGetProperty("matchId", out var matchIdProperty).Should().BeTrue();
        matchIdProperty.ValueKind.Should().NotBe(JsonValueKind.Null);
    }

    private static async Task<JsonElement> GetBracketAsync(HttpClient client, Guid tournamentId, Guid categoryId)
    {
        var response = await client.GetAsync($"/api/tournaments/{tournamentId}/bracket?categoryId={categoryId}");
        response.IsSuccessStatusCode.Should().BeTrue();
        return await response.ReadJsonAsync();
    }

    private static async Task PlayToFinishAsync(
        HttpClient client, Guid tournamentId, Guid matchId, Guid homeTeamId, Guid awayTeamId,
        int homeGoals, int awayGoals, Guid winnerTournamentTeamId)
    {
        (await client.PostAsync($"/api/tournaments/{tournamentId}/matches/{matchId}/start", null))
            .IsSuccessStatusCode.Should().BeTrue();

        for (var i = 0; i < homeGoals; i++)
            await TournamentFlow.AddGoalAsync(client, tournamentId, matchId, homeTeamId, minute: 10 + i);

        for (var i = 0; i < awayGoals; i++)
            await TournamentFlow.AddGoalAsync(client, tournamentId, matchId, awayTeamId, minute: 50 + i);

        var finishResponse = await client.PostAsJsonAsync($"/api/tournaments/{tournamentId}/matches/{matchId}/finish",
            new MatchesController.FinishMatchRequest(MatchResultType.Regular, winnerTournamentTeamId));
        finishResponse.IsSuccessStatusCode.Should().BeTrue();
    }
}

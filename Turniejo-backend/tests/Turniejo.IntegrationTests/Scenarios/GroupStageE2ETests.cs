using System.Net.Http.Json;
using FluentAssertions;
using Turniejo.Api.Controllers;
using Turniejo.IntegrationTests.Infrastructure;
using Turniejo.Matches.Domain.Enums;
using Turniejo.Tournaments.Application.Tournaments.CreateTournament;
using Turniejo.Tournaments.Domain.Enums;

namespace Turniejo.IntegrationTests.Scenarios;

// Pełny łańcuch spec #36: create -> teams -> schedule -> live scoring -> tabela grupowa,
// przez prawdziwe HTTP na żywej bazie, żadnych mocków poza Keycloakiem i JWT.
public sealed class GroupStageE2ETests(IntegrationTestWebApplicationFactory factory) : IntegrationTestBase(factory)
{
    [Fact]
    public async Task GroupStage_EndToEnd_CreatesSchedule_ScoresMatch_AndUpdatesStandings()
    {
        var organizer = await TournamentFlow.RegisterAndAuthenticateAsync(Factory, "organizer@integration.test");
        var now = DateTimeOffset.UtcNow;

        var organizationId = await TournamentFlow.CreateOrganizationAsync(organizer, "FC Integration Org");

        var createTournament = new CreateTournamentCommand(organizationId, "Integration Cup", now, now.AddDays(2), 16, null);
        var tournamentResponse = await organizer.PostAsJsonAsync("/api/tournaments", createTournament);
        tournamentResponse.IsSuccessStatusCode.Should().BeTrue();
        var tournamentId = (await tournamentResponse.ReadJsonAsync()).GetGuid("tournamentId");

        (await organizer.PatchAsJsonAsync($"/api/tournaments/{tournamentId}/format",
                new TournamentsController.UpdateTournamentFormatRequest(TournamentFormat.Group)))
            .IsSuccessStatusCode.Should().BeTrue();

        var categoryId = await TournamentFlow.AddCategoryAsync(organizer, tournamentId, "Open");

        var groupResponse = await organizer.PostAsJsonAsync($"/api/tournaments/{tournamentId}/groups",
            new TournamentsController.AddTournamentGroupRequest(categoryId, "Group A", 1));
        groupResponse.IsSuccessStatusCode.Should().BeTrue();
        var groupId = (await groupResponse.ReadJsonAsync()).GetGuid("tournamentGroupId");

        var homeTeamId = await TournamentFlow.CreateTeamAsync(organizer, organizationId, "Home FC");
        var awayTeamId = await TournamentFlow.CreateTeamAsync(organizer, organizationId, "Away FC");

        var homeTournamentTeamId = await TournamentFlow.AddTeamToTournamentAsync(organizer, tournamentId, homeTeamId, categoryId);
        var awayTournamentTeamId = await TournamentFlow.AddTeamToTournamentAsync(organizer, tournamentId, awayTeamId, categoryId);

        await TournamentFlow.AssignTeamToGroupAsync(organizer, tournamentId, homeTournamentTeamId, groupId);
        await TournamentFlow.AssignTeamToGroupAsync(organizer, tournamentId, awayTournamentTeamId, groupId);

        await TournamentFlow.AcceptRegistrationAsync(organizer, tournamentId, homeTournamentTeamId);
        await TournamentFlow.AcceptRegistrationAsync(organizer, tournamentId, awayTournamentTeamId);

        await TournamentFlow.ChangeStatusAsync(organizer, tournamentId, TournamentStatus.RegistrationOpen);
        await TournamentFlow.ChangeStatusAsync(organizer, tournamentId, TournamentStatus.RegistrationClosed);

        var pitchId = await TournamentFlow.AddPitchAsync(organizer, tournamentId, "Pitch 1");

        var scheduleResponse = await organizer.PostAsJsonAsync($"/api/tournaments/{tournamentId}/schedule/generate",
            new MatchesController.GenerateScheduleRequest(now, now.AddHours(3), [pitchId]));
        scheduleResponse.IsSuccessStatusCode.Should().BeTrue();
        (await scheduleResponse.ReadJsonAsync()).GetInt("matchesCreated").Should().Be(1);

        var scheduleGet = await organizer.GetAsync($"/api/tournaments/{tournamentId}/schedule");
        scheduleGet.IsSuccessStatusCode.Should().BeTrue();
        var matches = (await scheduleGet.ReadJsonAsync()).EnumerateArray().ToList();
        matches.Should().ContainSingle();
        var matchId = matches[0].GetGuid("id");

        (await organizer.PostAsync($"/api/tournaments/{tournamentId}/matches/{matchId}/start", null))
            .IsSuccessStatusCode.Should().BeTrue();

        await TournamentFlow.AddGoalAsync(organizer, tournamentId, matchId, homeTournamentTeamId, 10);
        await TournamentFlow.AddGoalAsync(organizer, tournamentId, matchId, homeTournamentTeamId, 35);
        await TournamentFlow.AddGoalAsync(organizer, tournamentId, matchId, awayTournamentTeamId, 60);

        var scoreAfterGoals = await organizer.GetAsync($"/api/tournaments/{tournamentId}/matches/{matchId}/score");
        scoreAfterGoals.IsSuccessStatusCode.Should().BeTrue();
        var liveScore = await scoreAfterGoals.ReadJsonAsync();
        liveScore.GetInt("homeScore").Should().Be(2);
        liveScore.GetInt("awayScore").Should().Be(1);

        // "Snapshot po reconnect aktualny" — GET live-panel to dokładnie to, co klient odpytuje po
        // ponownym połączeniu SignalR, żeby dogonić stan pominięty w trakcie rozłączenia. Czyta z bazy
        // przy każdym wywołaniu (bez cache), więc musi już odzwierciedlać obie bramki i live status.
        var livePanelResponse = await organizer.GetAsync($"/api/tournaments/{tournamentId}/matches/{matchId}/live-panel");
        livePanelResponse.IsSuccessStatusCode.Should().BeTrue();
        var livePanel = await livePanelResponse.ReadJsonAsync();
        livePanel.GetInt("homeScore").Should().Be(2);
        livePanel.GetInt("awayScore").Should().Be(1);
        livePanel.GetString("status").Should().Be("Live");
        // MatchStarted + 3x Goal.
        livePanel.GetProperty("events").GetArrayLength().Should().Be(4);

        var finishResponse = await organizer.PostAsJsonAsync($"/api/tournaments/{tournamentId}/matches/{matchId}/finish",
            new MatchesController.FinishMatchRequest(MatchResultType.Regular, homeTournamentTeamId));
        finishResponse.IsSuccessStatusCode.Should().BeTrue();

        // MatchFinished (przeliczenie tabeli) idzie przez outbox, nie synchronicznie z HTTP-callem.
        await FlushOutboxAsync();

        var standingsResponse = await organizer.GetAsync($"/api/tournaments/{tournamentId}/standings?groupId={groupId}");
        standingsResponse.IsSuccessStatusCode.Should().BeTrue();
        var standings = (await standingsResponse.ReadJsonAsync()).EnumerateArray().ToList();
        standings.Should().HaveCount(2);

        var homeStanding = standings.Single(s => s.GetGuid("tournamentTeamId") == homeTournamentTeamId);
        var awayStanding = standings.Single(s => s.GetGuid("tournamentTeamId") == awayTournamentTeamId);

        homeStanding.GetInt("points").Should().Be(3);
        homeStanding.GetInt("wins").Should().Be(1);
        homeStanding.GetInt("goalsFor").Should().Be(2);
        homeStanding.GetInt("goalsAgainst").Should().Be(1);
        homeStanding.GetInt("goalDifference").Should().Be(1);

        awayStanding.GetInt("points").Should().Be(0);
        awayStanding.GetInt("losses").Should().Be(1);
        awayStanding.GetInt("goalsFor").Should().Be(1);
        awayStanding.GetInt("goalsAgainst").Should().Be(2);
    }
}

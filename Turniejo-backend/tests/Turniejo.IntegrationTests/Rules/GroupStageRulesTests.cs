using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Turniejo.IntegrationTests.Infrastructure;
using Turniejo.Tournaments.Domain.Enums;

namespace Turniejo.IntegrationTests.Rules;

/// <summary>Arkusz QA „Grupy”: reguły, które w ręcznym przebiegu wyszły OK.</summary>
public sealed class GroupStageRulesTests(IntegrationTestWebApplicationFactory factory) : IntegrationTestBase(factory)
{
    private const int Regular = 2, Walkover = 5;

    private static string T(TournamentScenario s) => $"/api/tournaments/{s.TournamentId}";
    private static string M(TournamentScenario s, Guid matchId) => $"{T(s)}/matches/{matchId}";

    private static async Task<List<JsonElement>> TableAsync(TournamentScenario s, string group = "A") =>
        (await (await s.Organizer.GetAsync($"{T(s)}/standings?groupId={s.Group(group)}")).ReadJsonAsync()).EnumerateArray().ToList();

    private static async Task<JsonElement> RowAsync(TournamentScenario s, string team) =>
        (await TableAsync(s)).Single(r => r.GetGuid("tournamentTeamId") == s.Team(team));

    private static async Task<string> StatusAsync(TournamentScenario s) => (await s.TournamentAsync()).GetString("status");

    private static async Task<Guid> MatchIdAsync(TournamentScenario s, string home, string away) =>
        (await s.MatchBetweenAsync(home, away)).GetGuid("id");

    /// <summary>Wynik z perspektywy drużyn (nie gospodarza w terminarzu).</summary>
    private static async Task PlayAsync(TournamentScenario s, string a, string b, int goalsA, int goalsB)
    {
        var match = await s.MatchBetweenAsync(a, b);
        var aIsHome = match.GetGuid("homeTournamentTeamId") == s.Team(a);
        await s.PlayAsync(match.GetGuid("id"), aIsHome ? goalsA : goalsB, aIsHome ? goalsB : goalsA);
    }

    [Fact]
    public async Task GRP_003_TeamsAreAssignedToTheirGroups()
    {
        var s = await TournamentScenario.GroupsAsync(Factory, groups: 2, teamsPerGroup: 4, schedule: false);

        var teams = (await (await s.Organizer.GetAsync($"{T(s)}/teams")).ReadJsonAsync()).EnumerateArray().ToList();

        teams.Count(t => t.GetGuidOrNull("groupId") == s.Group("A")).Should().Be(4);
        teams.Count(t => t.GetGuidOrNull("groupId") == s.Group("B")).Should().Be(4);
    }

    [Fact]
    public async Task GRP_004_GroupWithTeams_CannotBeDeleted()
    {
        var s = await TournamentScenario.GroupsAsync(Factory, groups: 2, teamsPerGroup: 2, schedule: false);

        (await s.Organizer.DeleteAsync($"{T(s)}/groups/{s.Group("B")}")).IsSuccessStatusCode.Should().BeFalse();
    }

    [Fact]
    public async Task GRP_005_TieBreakerOrder_IsSaved_AndDuplicateOrderIsRejected()
    {
        var s = await TournamentScenario.GroupsAsync(Factory, teamsPerGroup: 2, schedule: false);
        var path = $"{T(s)}/tiebreaker-rules";

        (await s.Organizer.PutAsJsonAsync(path, new { rules = new[] { new { order = 1, type = 3 }, new { order = 2, type = 1 } } }))
            .IsSuccessStatusCode.Should().BeTrue();
        (await s.Organizer.PutAsJsonAsync(path, new { rules = new[] { new { order = 1, type = 3 }, new { order = 1, type = 1 } } }))
            .IsSuccessStatusCode.Should().BeFalse();

        var saved = (await (await s.Organizer.GetAsync(path)).ReadJsonAsync()).EnumerateArray().OrderBy(r => r.GetInt("order")).ToList();
        saved.Select(r => r.GetString("type")).Should().Equal("GoalDifference", "HeadToHeadPoints");
    }

    [Fact]
    public async Task GRP_007_GroupWithOneTeam_CannotGetASchedule()
    {
        var s = await TournamentScenario.GroupsAsync(Factory, groups: 2, teamsPerGroup: 1, schedule: false);

        var response = await s.Organizer.PostAsJsonAsync($"{T(s)}/schedule/generate",
            new { startAt = s.StartsAt, endAt = s.StartsAt.AddHours(12), pitchIds = new[] { s.PitchId } });

        response.IsSuccessStatusCode.Should().BeFalse();
        (await StatusAsync(s)).Should().Be("RegistrationClosed");
    }

    [Fact]
    public async Task GRP_009_TwoGroupsOfFour_OnTwoPitches_GiveTwelveRoundRobinMatches()
    {
        var s = await TournamentScenario.GroupsAsync(Factory, groups: 2, teamsPerGroup: 4, schedule: false);
        var second = await TournamentFlow.AddPitchAsync(s.Organizer, s.TournamentId, "Boisko 2");

        (await s.Organizer.PostAsJsonAsync($"{T(s)}/schedule/generate",
                new { startAt = s.StartsAt, endAt = s.StartsAt.AddHours(12), pitchIds = new[] { s.PitchId, second } }))
            .IsSuccessStatusCode.Should().BeTrue();

        var matches = await s.ScheduleAsync();
        matches.Should().HaveCount(12);
        matches.Select(m => string.Join("-", new[] { m.GetGuid("homeTournamentTeamId"), m.GetGuid("awayTournamentTeamId") }.Order()))
            .Should().OnlyHaveUniqueItems();
        foreach (var slot in matches.GroupBy(m => m.GetString("scheduledAt")))
            slot.SelectMany(m => new[] { m.GetGuid("homeTournamentTeamId"), m.GetGuid("awayTournamentTeamId") }).Should().OnlyHaveUniqueItems();
        matches.Select(m => m.GetGuid("pitchId")).Distinct().Should().HaveCount(2);
        (await StatusAsync(s)).Should().Be("ScheduleGenerated");
    }

    [Theory]
    [InlineData(3, 3)]
    [InlineData(5, 10)]
    public async Task GRP_010_OddGroup_GetsEveryPairOnce(int teams, int matches)
    {
        var s = await TournamentScenario.GroupsAsync(Factory, teamsPerGroup: teams, start: false);

        (await s.ScheduleAsync()).Should().HaveCount(matches);
    }

    [Fact]
    public async Task GRP_011_ClearingTheSchedule_RemovesMatchesAndRefereeAssignments()
    {
        var s = await TournamentScenario.GroupsAsync(Factory, teamsPerGroup: 3, start: false);
        var (referee, refereeId) = await s.JoinAsync("Referee");
        await s.AssignRefereeAsync(await MatchIdAsync(s, "T1", "T2"), refereeId);

        (await s.Organizer.DeleteAsync($"{T(s)}/schedule")).IsSuccessStatusCode.Should().BeTrue();

        (await s.ScheduleAsync()).Should().BeEmpty();
        (await StatusAsync(s)).Should().Be("RegistrationClosed");
        (await (await referee.GetAsync("/api/referee/matches")).ReadJsonAsync()).GetRawText().Should().NotContain(s.TournamentId.ToString());
    }

    [Fact]
    public async Task GRP_012_ScheduleCannotBeClearedAfterTheTournamentStarted()
    {
        var s = await TournamentScenario.GroupsAsync(Factory, teamsPerGroup: 3);

        (await s.Organizer.DeleteAsync($"{T(s)}/schedule")).IsSuccessStatusCode.Should().BeFalse();
        (await s.ScheduleAsync()).Should().HaveCount(3);
    }

    [Fact]
    public async Task GRP_013_StagesStepBackOneAtATime_AndTeamsStay()
    {
        var s = await TournamentScenario.GroupsAsync(Factory, teamsPerGroup: 3, schedule: false);

        await TournamentFlow.ChangeStatusAsync(s.Organizer, s.TournamentId, TournamentStatus.RegistrationOpen);
        await TournamentFlow.ChangeStatusAsync(s.Organizer, s.TournamentId, TournamentStatus.Draft);

        (await StatusAsync(s)).Should().Be("Draft");
        (await (await s.Organizer.GetAsync($"{T(s)}/teams")).ReadJsonAsync()).EnumerateArray().Should().HaveCount(3);
    }

    [Fact]
    public async Task GRP_015_GroupMatch_CanEndInADraw_WithAPointEach()
    {
        var s = await TournamentScenario.GroupsAsync(Factory, teamsPerGroup: 2);

        await PlayAsync(s, "T1", "T2", 1, 1);

        (await RowAsync(s, "T1")).GetInt("points").Should().Be(1);
        (await RowAsync(s, "T2")).GetInt("points").Should().Be(1);
    }

    [Fact]
    public async Task GRP_016_CustomPoints_AreUsedInTheTable()
    {
        var s = await TournamentScenario.GroupsAsync(Factory, teamsPerGroup: 3, start: false);
        (await s.Organizer.PutAsJsonAsync($"{T(s)}/settings",
                new { pointsForWin = 2, pointsForDraw = 1, pointsForLoss = 0, matchDurationMinutes = 30, breakDurationMinutes = 5, isThirdPlaceMatchEnabled = false, refereeEditWindowMinutes = 60, walkoverHomeScore = 3, walkoverAwayScore = 0, canCoachEditSquadAfterStart = false }))
            .IsSuccessStatusCode.Should().BeTrue();
        await s.StartAsync();

        await PlayAsync(s, "T1", "T2", 1, 0);
        await PlayAsync(s, "T1", "T3", 0, 0);

        (await RowAsync(s, "T1")).GetInt("points").Should().Be(3);
    }

    [Fact]
    public async Task GRP_018_Walkover_CountsInPointsAndGoals()
    {
        var s = await TournamentScenario.GroupsAsync(Factory, teamsPerGroup: 2);
        var match = await MatchIdAsync(s, "T1", "T2");
        (await s.Organizer.PostAsync($"{M(s, match)}/start", null)).IsSuccessStatusCode.Should().BeTrue();

        (await s.Organizer.PostAsJsonAsync($"{M(s, match)}/finish", new { resultType = Walkover, winnerTournamentTeamId = s.Team("T2") }))
            .IsSuccessStatusCode.Should().BeTrue();
        await FlushOutboxAsync();

        var winner = await RowAsync(s, "T2");
        winner.GetInt("points").Should().Be(3);
        winner.GetInt("goalsFor").Should().Be(3);
        (await RowAsync(s, "T1")).GetInt("goalsAgainst").Should().Be(3);
    }

    [Fact]
    public async Task GRP_019_CorrectingAFinishedMatch_RecalculatesTheTable()
    {
        var s = await TournamentScenario.GroupsAsync(Factory, teamsPerGroup: 2);
        var match = await s.MatchBetweenAsync("T1", "T2");
        var t1Home = match.GetGuid("homeTournamentTeamId") == s.Team("T1");
        await PlayAsync(s, "T1", "T2", 1, 0);

        (await s.Organizer.PatchAsJsonAsync($"{M(s, match.GetGuid("id"))}/correct-score",
                new { homeScore = t1Home ? 0 : 2, awayScore = t1Home ? 2 : 0, winnerTournamentTeamId = s.Team("T2") }))
            .IsSuccessStatusCode.Should().BeTrue();
        await FlushOutboxAsync();

        (await RowAsync(s, "T2")).GetInt("points").Should().Be(3);
        (await RowAsync(s, "T1")).GetInt("points").Should().Be(0);
    }

    [Fact]
    public async Task GRP_020_GRP_021_WithdrawnTeam_KeepsPlayedResults_AndItsCancelledMatchesDoNotCount()
    {
        var s = await TournamentScenario.GroupsAsync(Factory, teamsPerGroup: 3);
        await PlayAsync(s, "T1", "T2", 2, 0);
        var unplayed = await MatchIdAsync(s, "T1", "T3");

        (await s.Organizer.PatchAsJsonAsync($"{T(s)}/tournament-teams/{s.Team("T1")}/registration-status", new { registrationStatus = 4 }))
            .IsSuccessStatusCode.Should().BeTrue();
        await FlushOutboxAsync();

        (await s.ScheduleAsync()).Single(m => m.GetGuid("id") == unplayed).GetString("status").Should().Be("Cancelled");
        (await s.MatchBetweenAsync("T1", "T2")).GetString("status").Should().Be("Finished");
        var t3 = await RowAsync(s, "T3");
        t3.GetInt("matchesPlayed").Should().Be(0);
        t3.GetInt("points").Should().Be(0);
        (await RowAsync(s, "T2")).GetInt("goalsAgainst").Should().Be(2);
    }

    [Fact]
    public async Task GRP_025_ResultWithoutClock_UpdatesTheTable()
    {
        var s = await TournamentScenario.GroupsAsync(Factory, teamsPerGroup: 2);
        var match = await s.MatchBetweenAsync("T1", "T2");
        var t1Home = match.GetGuid("homeTournamentTeamId") == s.Team("T1");

        (await s.Organizer.PostAsJsonAsync($"{M(s, match.GetGuid("id"))}/record-result",
                new { homeScore = t1Home ? 2 : 1, awayScore = t1Home ? 1 : 2, resultType = Regular, winnerTournamentTeamId = s.Team("T1") }))
            .IsSuccessStatusCode.Should().BeTrue();
        await FlushOutboxAsync();

        var row = await RowAsync(s, "T1");
        row.GetInt("points").Should().Be(3);
        (row.GetInt("goalsFor"), row.GetInt("goalsAgainst")).Should().Be((2, 1));
    }

    [Fact]
    public async Task GRP_026_PenaltiesAfterAGroupDraw_StillCountAsADraw()
    {
        var s = await TournamentScenario.GroupsAsync(Factory, teamsPerGroup: 2);
        var match = (await s.MatchBetweenAsync("T1", "T2")).GetGuid("id");
        (await s.Organizer.PostAsync($"{M(s, match)}/start", null)).IsSuccessStatusCode.Should().BeTrue();
        await TournamentFlow.AddGoalAsync(s.Organizer, s.TournamentId, match, s.Team("T1"), 10);
        await TournamentFlow.AddGoalAsync(s.Organizer, s.TournamentId, match, s.Team("T2"), 20);
        var home = (await s.MatchBetweenAsync("T1", "T2")).GetGuid("homeTournamentTeamId");

        (await s.Organizer.PostAsJsonAsync($"{M(s, match)}/finish",
                new { resultType = 4, winnerTournamentTeamId = home, homePenaltyScore = 4, awayPenaltyScore = 3 }))
            .IsSuccessStatusCode.Should().BeTrue();
        await FlushOutboxAsync();

        (await RowAsync(s, "T1")).GetInt("draws").Should().Be(1);
        (await RowAsync(s, "T2")).GetInt("draws").Should().Be(1);
        (await RowAsync(s, "T1")).GetInt("points").Should().Be(1);
        (await s.MatchBetweenAsync("T1", "T2")).GetInt("homePenaltyScore").Should().Be(4);
    }

    // QA v3: PUC-033b (przepisany na turniej grupowy - w pucharze przed startem drużyna ma jeden mecz).
    [Fact]
    public async Task PUC_033b_MovingAMatchToATimeWhenOneOfItsTeamsPlays_IsRejected()
    {
        var s = await TournamentScenario.GroupsAsync(Factory, teamsPerGroup: 3, start: false);
        var busy = await s.MatchBetweenAsync("T1", "T2");
        var moved = (await s.MatchBetweenAsync("T1", "T3")).GetGuid("id");
        var otherPitch = await TournamentFlow.AddPitchAsync(s.Organizer, s.TournamentId, "Boisko 2");

        var response = await s.Organizer.PutAsJsonAsync($"{M(s, moved)}/schedule",
            new { pitchId = otherPitch, scheduledAt = DateTimeOffset.Parse(busy.GetString("scheduledAt")) });

        response.IsSuccessStatusCode.Should().BeFalse();
        (await response.Content.ReadAsStringAsync()).Should().Contain("One of the teams already plays another match at this time");
    }
}

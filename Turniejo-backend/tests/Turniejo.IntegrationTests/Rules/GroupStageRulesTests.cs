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
    public async Task GRP_004_GroupWithOnlyWithdrawnTeams_CanBeDeleted()
    {
        var s = await TournamentScenario.GroupsAsync(Factory, groups: 2, teamsPerGroup: 2, schedule: false);
        (await s.Organizer.PatchAsJsonAsync($"{T(s)}/tournament-teams/{s.Team("T3")}/registration-status", new { registrationStatus = 4 }))
            .IsSuccessStatusCode.Should().BeTrue();
        (await s.Organizer.PatchAsJsonAsync($"{T(s)}/tournament-teams/{s.Team("T4")}/registration-status", new { registrationStatus = 4 }))
            .IsSuccessStatusCode.Should().BeTrue();

        (await s.Organizer.DeleteAsync($"{T(s)}/groups/{s.Group("B")}")).IsSuccessStatusCode.Should().BeTrue();

        // Wycofane zgłoszenia nie wskazują już na usuniętą grupę.
        var teams = (await (await s.Organizer.GetAsync($"{T(s)}/teams")).ReadJsonAsync()).EnumerateArray().ToList();
        teams.Where(t => t.GetGuid("id") == s.Team("T3") || t.GetGuid("id") == s.Team("T4"))
            .Should().HaveCount(2).And.OnlyContain(t => t.GetGuidOrNull("groupId") == null);
        var groups = (await (await s.Organizer.GetAsync($"{T(s)}/groups")).ReadJsonAsync()).EnumerateArray();
        groups.Select(g => g.GetGuid("id")).Should().NotContain(s.Group("B"));
    }

    [Fact]
    public async Task GRP_002_NewCategory_GetsTheRequestedNumberOfGroups()
    {
        var s = await TournamentScenario.GroupsAsync(Factory, teamsPerGroup: 2, schedule: false);

        var response = await s.Organizer.PostAsJsonAsync($"{T(s)}/categories",
            new { name = "U12", minBirthDate = (DateTime?)null, maxBirthDate = (DateTime?)null, order = 2, groupCount = 3 });
        response.IsSuccessStatusCode.Should().BeTrue();
        var categoryId = (await response.ReadJsonAsync()).GetGuid("tournamentCategoryId");

        var groups = (await (await s.Organizer.GetAsync($"{T(s)}/groups")).ReadJsonAsync()).EnumerateArray()
            .Where(g => g.GetGuid("categoryId") == categoryId)
            .OrderBy(g => g.GetInt("order"))
            .Select(g => g.GetString("name"));
        groups.Should().Equal("Grupa A", "Grupa B", "Grupa C");
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
    public async Task GRP_005_NewTournament_HasDefaultTieBreakerOrder()
    {
        var s = await TournamentScenario.GroupsAsync(Factory, teamsPerGroup: 2, schedule: false);

        var rules = (await (await s.Organizer.GetAsync($"{T(s)}/tiebreaker-rules")).ReadJsonAsync()).EnumerateArray()
            .OrderBy(r => r.GetInt("order")).Select(r => r.GetString("type"));

        rules.Should().Equal("HeadToHeadPoints", "HeadToHeadGoalDiff", "GoalDifference", "GoalsFor", "Wins");
    }

    [Fact]
    public async Task GRP_005_WithoutSavedRules_EqualPointsAreResolvedByGoalDifference()
    {
        var s = await TournamentScenario.GroupsAsync(Factory, teamsPerGroup: 3);

        // T1 i T2 po 4 punkty, remis między sobą, T1 ma lepszy bilans.
        await PlayAsync(s, "T1", "T2", 0, 0);
        await PlayAsync(s, "T1", "T3", 5, 0);
        await PlayAsync(s, "T2", "T3", 1, 0);

        var order = (await TableAsync(s)).OrderBy(r => r.GetInt("position")).Select(r => r.GetGuid("tournamentTeamId"));
        order.Should().Equal(s.Team("T1"), s.Team("T2"), s.Team("T3"));
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

    // Opóźnienie na boisku: przesunięcie meczu o cały slot bez przesuwania kolejnych trafia na następny mecz.
    [Fact]
    public async Task GRP_Schedule_DelayingAMatchIntoTheNextOne_WithoutShiftingFollowing_IsRejected()
    {
        var s = await TournamentScenario.GroupsAsync(Factory, teamsPerGroup: 4, start: false);
        var (first, gap) = await FirstMatchAndGapAsync(s);

        var response = await s.Organizer.PutAsJsonAsync($"{M(s, first.GetGuid("id"))}/schedule",
            new { pitchId = s.PitchId, scheduledAt = DateTimeOffset.Parse(first.GetString("scheduledAt")) + gap });

        response.IsSuccessStatusCode.Should().BeFalse();
        (await response.Content.ReadAsStringAsync()).Should().Contain("Another match is already scheduled on this pitch at this time");
    }

    // Każdy mecz ląduje dokładnie na starym terminie następnego - sprawdza też, że zapis nie potyka się
    // o unikalny indeks (boisko, godzina) w trakcie przesuwania.
    [Fact]
    public async Task GRP_Schedule_ShiftingFollowing_MovesEveryLaterMatchOnThePitchByTheSameAmount()
    {
        var s = await TournamentScenario.GroupsAsync(Factory, teamsPerGroup: 4, start: false);
        var before = (await s.ScheduleAsync()).ToDictionary(m => m.GetGuid("id"), m => DateTimeOffset.Parse(m.GetString("scheduledAt")));
        var (first, gap) = await FirstMatchAndGapAsync(s);

        var response = await s.Organizer.PutAsJsonAsync($"{M(s, first.GetGuid("id"))}/schedule",
            new { pitchId = s.PitchId, scheduledAt = before[first.GetGuid("id")] + gap, shiftFollowing = true });

        response.IsSuccessStatusCode.Should().BeTrue(await response.Content.ReadAsStringAsync());
        var after = (await s.ScheduleAsync()).ToDictionary(m => m.GetGuid("id"), m => DateTimeOffset.Parse(m.GetString("scheduledAt")));
        after.Should().HaveCount(6);
        foreach (var (id, at) in before)
            after[id].Should().Be(at + gap);
    }

    // Grupa 3 drużyn: każde dwa mecze mają wspólną drużynę. Boisko A: pierwszy mecz. Boisko B: drugi zaraz
    // po nim, trzeci jeszcze później. Opóźnienie pierwszego najeżdża na drugi na boisku B.
    [Fact]
    public async Task GRP_Schedule_ShiftThatHitsATeamOnAnotherPitch_AsksFirst_ThenMovesThatPitchToo()
    {
        var s = await TournamentScenario.GroupsAsync(Factory, teamsPerGroup: 3, start: false);
        var pitchB = await TournamentFlow.AddPitchAsync(s.Organizer, s.TournamentId, "Boisko 2");
        var (first, gap) = await FirstMatchAndGapAsync(s);
        var ordered = (await s.ScheduleAsync()).OrderBy(m => DateTimeOffset.Parse(m.GetString("scheduledAt"))).ToList();
        var t0 = DateTimeOffset.Parse(first.GetString("scheduledAt"));
        var (second, third) = (ordered[1].GetGuid("id"), ordered[2].GetGuid("id"));
        (await s.Organizer.PutAsJsonAsync($"{M(s, third)}/schedule", new { pitchId = pitchB, scheduledAt = t0 + gap * 3 }))
            .IsSuccessStatusCode.Should().BeTrue();
        (await s.Organizer.PutAsJsonAsync($"{M(s, second)}/schedule", new { pitchId = pitchB, scheduledAt = t0 + gap }))
            .IsSuccessStatusCode.Should().BeTrue();
        var body = new { pitchId = s.PitchId, scheduledAt = t0 + gap, shiftFollowing = true };

        var asked = await s.Organizer.PutAsJsonAsync($"{M(s, first.GetGuid("id"))}/schedule", body);

        ((int)asked.StatusCode).Should().Be(409);
        (await asked.Content.ReadAsStringAsync()).Should().Contain("Conflict.OtherPitches");
        DateTimeOffset At(List<JsonElement> list, Guid id) => DateTimeOffset.Parse(list.Single(m => m.GetGuid("id") == id).GetString("scheduledAt"));
        At(await s.ScheduleAsync(), first.GetGuid("id")).Should().Be(t0);

        var confirmed = await s.Organizer.PutAsJsonAsync($"{M(s, first.GetGuid("id"))}/schedule",
            new { body.pitchId, body.scheduledAt, shiftFollowing = true, shiftOtherPitches = true });

        confirmed.IsSuccessStatusCode.Should().BeTrue(await confirmed.Content.ReadAsStringAsync());
        var after = await s.ScheduleAsync();
        At(after, first.GetGuid("id")).Should().Be(t0 + gap);
        At(after, second).Should().Be(t0 + gap * 2);
        At(after, third).Should().Be(t0 + gap * 4);
    }

    private static async Task<(JsonElement First, TimeSpan Gap)> FirstMatchAndGapAsync(TournamentScenario s)
    {
        var ordered = (await s.ScheduleAsync()).OrderBy(m => DateTimeOffset.Parse(m.GetString("scheduledAt"))).ToList();
        var gap = DateTimeOffset.Parse(ordered[1].GetString("scheduledAt")) - DateTimeOffset.Parse(ordered[0].GetString("scheduledAt"));
        return (ordered[0], gap);
    }

    // Tabela to projekcja liczona po zakończonym meczu. Zanim pierwszy mecz grupy się skończy, drużyny
    // grupy mają być w tabeli z zerami, a nie "Brak drużyn w grupie".
    [Fact]
    public async Task GRP_Standings_ListAllGroupTeams_BeforeTheFirstMatchEnds()
    {
        var s = await TournamentScenario.GroupsAsync(Factory, teamsPerGroup: 4);

        var table = await TableAsync(s);

        table.Select(r => r.GetGuid("tournamentTeamId")).Should()
            .BeEquivalentTo(new[] { s.Team("T1"), s.Team("T2"), s.Team("T3"), s.Team("T4") });
        table.Should().OnlyContain(r => r.GetInt("matchesPlayed") == 0 && r.GetInt("points") == 0);
        table.Select(r => r.GetInt("position")).Should().Equal(1, 2, 3, 4);
    }

    [Fact]
    public async Task GRP_PublicStandings_ListAllGroupTeams_BeforeTheFirstMatchEnds()
    {
        var s = await TournamentScenario.GroupsAsync(Factory, teamsPerGroup: 3);
        var slug = (await s.TournamentAsync()).GetString("slug");
        (await s.Organizer.PutAsJsonAsync($"{T(s)}/public-settings",
                new { isPublic = true, showTeams = true, showPlayers = true, showSchedule = true, showStandings = true, showBracket = true, showLiveMatches = true, showStats = true }))
            .IsSuccessStatusCode.Should().BeTrue();

        var table = (await (await CreateClient().GetAsync($"/public/tournaments/{slug}/standings?groupId={s.Group("A")}")).ReadJsonAsync())
            .EnumerateArray().ToList();

        table.Should().HaveCount(3);
        table.Select(r => r.GetInt("position")).Should().Equal(1, 2, 3);
    }

    // Wynik pierwszego meczu nie może zgubić drużyn, które jeszcze nie grały.
    [Fact]
    public async Task GRP_Standings_AfterTheFirstMatch_KeepTeamsThatHaveNotPlayed()
    {
        var s = await TournamentScenario.GroupsAsync(Factory, teamsPerGroup: 4);
        await PlayAsync(s, "T1", "T2", 2, 0);
        await FlushOutboxAsync();

        var table = await TableAsync(s);

        table.Should().HaveCount(4);
        table.First().GetGuid("tournamentTeamId").Should().Be(s.Team("T1"));
        table.Select(r => r.GetInt("position")).Should().Equal(1, 2, 3, 4);
    }

    // Mecze grupowe powstają z podziału na grupy w chwili generowania. Zmiana grupy albo nowa drużyna
    // po generowaniu rozjeżdżała mecze z grupami (mecze "Grupy A" z drużyną z Grupy B, błędne tabele).
    [Fact]
    public async Task GRP_031_TeamCannotChangeGroup_OnceScheduleIsGenerated()
    {
        var s = await TournamentScenario.GroupsAsync(Factory, groups: 2, teamsPerGroup: 2, start: false);

        var response = await s.Organizer.PatchAsJsonAsync($"{T(s)}/tournament-teams/{s.Team("T1")}/group",
            new Turniejo.Api.Controllers.TournamentsController.AssignTeamToGroupRequest(s.Group("B")));

        response.StatusCode.Should().Be(System.Net.HttpStatusCode.Conflict);
        (await response.Content.ReadAsStringAsync()).Should().Contain("Clear the schedule");
        var teams = (await (await s.Organizer.GetAsync($"{T(s)}/teams")).ReadJsonAsync()).EnumerateArray();
        teams.Single(t => t.GetGuid("id") == s.Team("T1")).GetGuid("groupId").Should().Be(s.Group("A"));
    }

    [Fact]
    public async Task GRP_032_NewTeamCannotBeAccepted_OnceScheduleIsGenerated()
    {
        var s = await TournamentScenario.GroupsAsync(Factory, teamsPerGroup: 2, start: false);
        var teamId = await TournamentFlow.CreateTeamAsync(s.Organizer, s.OrganizationId, "Spóźnieni");
        var tt = await TournamentFlow.AddTeamToTournamentAsync(s.Organizer, s.TournamentId, teamId, s.CategoryId);

        var response = await s.Organizer.PatchAsJsonAsync($"{T(s)}/tournament-teams/{tt}/registration-status",
            new { registrationStatus = 2 });

        response.StatusCode.Should().Be(System.Net.HttpStatusCode.Conflict);
        (await response.Content.ReadAsStringAsync()).Should().Contain("Clear the schedule");
    }

    [Fact]
    public async Task GRP_033_GroupsCanChangeAgain_AfterClearingTheSchedule()
    {
        var s = await TournamentScenario.GroupsAsync(Factory, groups: 2, teamsPerGroup: 2, start: false);
        (await s.Organizer.DeleteAsync($"{T(s)}/schedule")).IsSuccessStatusCode.Should().BeTrue();

        await TournamentFlow.AssignTeamToGroupAsync(s.Organizer, s.TournamentId, s.Team("T1"), s.Group("B"));
    }
}

using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Turniejo.IntegrationTests.Infrastructure;

namespace Turniejo.IntegrationTests.Rules;

/// <summary>
/// Arkusz QA „Mecz na żywo”: reguły, które w ręcznym przebiegu wyszły OK. Mecz T1-T2 trwa,
/// zawodnicy: Adam i Bartek (T1), Cezary (T2), Dawid (T3 - spoza meczu).
/// </summary>
public sealed class LiveMatchRulesTests(IntegrationTestWebApplicationFactory factory) : IntegrationTestBase(factory)
{
    private const int Goal = 5, OwnGoal = 6, PenaltyGoal = 7, Yellow = 8, Red = 9, Regular = 2;

    private sealed record Live(TournamentScenario S, Guid Match, string Path, Guid Adam, Guid Bartek, Guid Cezary, Guid Dawid)
    {
        public HttpClient Org => S.Organizer;
        public Guid T1 => S.Team("T1");
        public Guid T2 => S.Team("T2");
    }

    private async Task<Live> LiveAsync(bool start = true)
    {
        var s = await TournamentScenario.GroupsAsync(Factory, teamsPerGroup: 4);
        var (_, adam) = await s.CreatePlayerAsync("T1", "Adam", "A");
        var (_, bartek) = await s.CreatePlayerAsync("T1", "Bartek", "B");
        var (_, cezary) = await s.CreatePlayerAsync("T2", "Cezary", "C");
        var (_, dawid) = await s.CreatePlayerAsync("T3", "Dawid", "D");
        var match = (await s.MatchBetweenAsync("T1", "T2")).GetGuid("id");
        var path = $"/api/tournaments/{s.TournamentId}/matches/{match}";
        if (start)
            (await s.Organizer.PostAsync($"{path}/start", null)).IsSuccessStatusCode.Should().BeTrue();
        return new Live(s, match, path, adam, bartek, cezary, dawid);
    }

    private static Task<HttpResponseMessage> GoalAsync(Live l, Guid team, Guid? player, int type = Goal, Guid? assist = null, int minute = 10) =>
        l.Org.PostAsJsonAsync($"{l.Path}/goals", new { tournamentTeamId = team, playerId = player, assistPlayerId = assist, goalType = type, minute, second = (int?)null });

    private static Task<HttpResponseMessage> CardAsync(Live l, Guid team, Guid player, int type, int minute = 20) =>
        l.Org.PostAsJsonAsync($"{l.Path}/cards", new { tournamentTeamId = team, playerId = player, cardType = type, minute, second = (int?)null });

    private static async Task<Guid> EventIdAsync(HttpResponseMessage response)
    {
        response.IsSuccessStatusCode.Should().BeTrue(await response.Content.ReadAsStringAsync());
        return (await response.ReadJsonAsync()).GetGuid("matchEventId");
    }

    private static async Task<(int Home, int Away)> ScoreAsync(Live l)
    {
        var match = (await l.S.ScheduleAsync()).Single(m => m.GetGuid("id") == l.Match);
        return (match.GetInt("homeScore"), match.GetInt("awayScore"));
    }

    private static async Task<int> ScoreOfAsync(Live l, Guid team)
    {
        var match = (await l.S.ScheduleAsync()).Single(m => m.GetGuid("id") == l.Match);
        return match.GetGuid("homeTournamentTeamId") == team ? match.GetInt("homeScore") : match.GetInt("awayScore");
    }

    private static async Task<List<JsonElement>> EventsAsync(Live l) =>
        (await (await l.Org.GetAsync($"{l.Path}/events")).ReadJsonAsync()).EnumerateArray().ToList();

    private static async Task<List<JsonElement>> RankingAsync(Live l, string kind) =>
        (await (await l.Org.GetAsync($"/api/tournaments/{l.S.TournamentId}/stats/{kind}")).ReadJsonAsync()).EnumerateArray().ToList();

    private static int CountFor(List<JsonElement> ranking, Guid player) =>
        ranking.Where(r => r.GetGuid("playerId") == player).Select(r => r.GetInt("count")).FirstOrDefault();

    [Fact]
    public async Task MNZ_001_RefereeSeesAssignedMatches_AndOpenMatchesSeparately()
    {
        var l = await LiveAsync(start: false);
        var (referee, refereeId) = await l.S.JoinAsync("Referee");
        await l.S.AssignRefereeAsync(l.Match, refereeId);

        var assigned = (await (await referee.GetAsync("/api/referee/matches")).ReadJsonAsync()).EnumerateArray().Select(m => m.GetGuid("id")).ToList();
        var open = (await (await referee.GetAsync("/api/referee/matches/open")).ReadJsonAsync()).EnumerateArray().Select(m => m.GetGuid("id")).ToList();

        assigned.Should().Equal(l.Match);
        open.Should().NotContain(l.Match).And.HaveCount(5);
    }

    [Fact]
    public async Task MNZ_003_PausedClockStops_AndResumesFromTheSameValue()
    {
        var l = await LiveAsync();

        (await l.Org.PostAsync($"{l.Path}/pause", null)).IsSuccessStatusCode.Should().BeTrue();
        var paused = await (await l.Org.GetAsync($"{l.Path}/clock")).ReadJsonAsync();
        await Task.Delay(1500);
        var stillPaused = await (await l.Org.GetAsync($"{l.Path}/clock")).ReadJsonAsync();
        (await l.Org.PostAsync($"{l.Path}/resume", null)).IsSuccessStatusCode.Should().BeTrue();
        var resumed = await (await l.Org.GetAsync($"{l.Path}/clock")).ReadJsonAsync();

        paused.GetString("status").Should().Be("Paused");
        stillPaused.GetInt("elapsedSeconds").Should().Be(paused.GetInt("elapsedSeconds"));
        resumed.GetString("status").Should().Be("Live");
        resumed.GetInt("elapsedSeconds").Should().BeInRange(paused.GetInt("elapsedSeconds"), paused.GetInt("elapsedSeconds") + 1);
    }

    [Fact]
    public async Task MNZ_006_MNZ_008_GoalsWithScorerAndAssist_CountInStats_IncludingPenalties()
    {
        var l = await LiveAsync();

        await EventIdAsync(await GoalAsync(l, l.T1, l.Adam, assist: l.Bartek));
        await EventIdAsync(await GoalAsync(l, l.T1, l.Adam, type: PenaltyGoal, minute: 30));
        await FlushOutboxAsync();

        (await ScoreOfAsync(l, l.T1)).Should().Be(2);
        CountFor(await RankingAsync(l, "top-scorers"), l.Adam).Should().Be(2);
        CountFor(await RankingAsync(l, "top-assists"), l.Bartek).Should().Be(1);
        (await EventsAsync(l)).Should().Contain(e => e.GetString("type") == "PenaltyGoal");
    }

    [Fact]
    public async Task MNZ_007_OwnGoal_ScoresForTheOtherTeam_AndNotForTheScorer()
    {
        var l = await LiveAsync();

        await EventIdAsync(await GoalAsync(l, l.T1, l.Adam, type: OwnGoal));
        await FlushOutboxAsync();

        (await ScoreOfAsync(l, l.T2)).Should().Be(1);
        (await ScoreOfAsync(l, l.T1)).Should().Be(0);
        CountFor(await RankingAsync(l, "top-scorers"), l.Adam).Should().Be(0);
    }

    [Fact]
    public async Task MNZ_009_PlayerFromOutsideTheMatch_IsRejected()
    {
        var l = await LiveAsync();

        (await GoalAsync(l, l.T1, l.Dawid)).IsSuccessStatusCode.Should().BeFalse();
        (await CardAsync(l, l.T1, l.Dawid, Yellow)).IsSuccessStatusCode.Should().BeFalse();
        (await GoalAsync(l, l.T1, l.Cezary)).IsSuccessStatusCode.Should().BeFalse("zawodnik drużyny gości nie strzela dla gospodarzy");
        (await ScoreAsync(l)).Should().Be((0, 0));
    }

    [Fact]
    public async Task MNZ_010_MNZ_033a_GoalWithoutScorer_CanGetItsScorerAndAssistLater()
    {
        var l = await LiveAsync();
        var goal = await EventIdAsync(await GoalAsync(l, l.T1, null));
        await FlushOutboxAsync();
        CountFor(await RankingAsync(l, "top-scorers"), l.Adam).Should().Be(0);

        (await l.Org.PatchAsJsonAsync($"{l.Path}/events/{goal}/players", new { playerId = l.Adam, assistPlayerId = l.Bartek }))
            .IsSuccessStatusCode.Should().BeTrue();
        await FlushOutboxAsync();

        (await ScoreOfAsync(l, l.T1)).Should().Be(1);
        CountFor(await RankingAsync(l, "top-scorers"), l.Adam).Should().Be(1);
        CountFor(await RankingAsync(l, "top-assists"), l.Bartek).Should().Be(1);
    }

    [Fact]
    public async Task MNZ_011_MNZ_013_MNZ_014_RedCardExcludesThePlayer_UntilItIsReverted()
    {
        var l = await LiveAsync();
        await EventIdAsync(await CardAsync(l, l.T1, l.Bartek, Yellow));
        (await EventsAsync(l)).Should().Contain(e => e.GetString("type") == "YellowCard" && e.GetGuidOrNull("playerId") == l.Bartek);

        var red = await EventIdAsync(await CardAsync(l, l.T1, l.Adam, Red));

        (await GoalAsync(l, l.T1, l.Adam)).IsSuccessStatusCode.Should().BeFalse();
        (await GoalAsync(l, l.T1, l.Bartek, assist: l.Adam)).IsSuccessStatusCode.Should().BeFalse();
        (await CardAsync(l, l.T1, l.Adam, Yellow)).IsSuccessStatusCode.Should().BeFalse();

        (await l.Org.DeleteAsync($"{l.Path}/events/{red}")).IsSuccessStatusCode.Should().BeTrue();
        await EventIdAsync(await GoalAsync(l, l.T1, l.Adam));
    }

    [Fact]
    public async Task MNZ_016_RevertedGoal_LowersTheScore_AndCannotBeRevertedOrEditedAgain()
    {
        var l = await LiveAsync();
        var goal = await EventIdAsync(await GoalAsync(l, l.T1, l.Adam));

        (await l.Org.DeleteAsync($"{l.Path}/events/{goal}")).IsSuccessStatusCode.Should().BeTrue();

        (await ScoreAsync(l)).Should().Be((0, 0));
        (await EventsAsync(l)).Should().NotContain(e => e.GetGuid("id") == goal);
        (await l.Org.DeleteAsync($"{l.Path}/events/{goal}")).IsSuccessStatusCode.Should().BeFalse();
        (await l.Org.PutAsJsonAsync($"{l.Path}/events/{goal}", new { type = Goal, minute = 12, second = (int?)null, playerId = l.Adam, relatedPlayerId = (Guid?)null }))
            .IsSuccessStatusCode.Should().BeFalse();
    }

    [Fact]
    public async Task MNZ_021_FinishedOrCancelledMatch_CannotBeStarted()
    {
        var l = await LiveAsync();
        (await l.Org.PostAsJsonAsync($"{l.Path}/finish", new { resultType = Regular, winnerTournamentTeamId = (Guid?)null })).IsSuccessStatusCode.Should().BeTrue();

        (await l.Org.PostAsync($"{l.Path}/start", null)).IsSuccessStatusCode.Should().BeFalse();

        var other = (await l.S.MatchBetweenAsync("T3", "T4")).GetGuid("id");
        (await l.Org.PatchAsJsonAsync($"/api/tournaments/{l.S.TournamentId}/tournament-teams/{l.S.Team("T3")}/registration-status", new { registrationStatus = 4 }))
            .IsSuccessStatusCode.Should().BeTrue();
        await FlushOutboxAsync();
        (await l.Org.PostAsync($"/api/tournaments/{l.S.TournamentId}/matches/{other}/start", null)).IsSuccessStatusCode.Should().BeFalse();
    }

    [Fact]
    public async Task MNZ_022_LiveMatch_KeepsItsTermButCanChangeReferee_FinishedMatchChangesNothing()
    {
        var l = await LiveAsync();
        var (_, refereeId) = await l.S.JoinAsync("Referee");
        var later = l.S.StartsAt.AddHours(5);

        (await l.Org.PutAsJsonAsync($"{l.Path}/schedule", new { pitchId = l.S.PitchId, scheduledAt = later })).IsSuccessStatusCode.Should().BeFalse();
        (await l.Org.PatchAsJsonAsync($"{l.Path}/referee", new { refereeId })).IsSuccessStatusCode.Should().BeTrue();

        (await l.Org.PostAsJsonAsync($"{l.Path}/finish", new { resultType = Regular, winnerTournamentTeamId = (Guid?)null })).IsSuccessStatusCode.Should().BeTrue();
        var pitch2 = await TournamentFlow.AddPitchAsync(l.Org, l.S.TournamentId, "Boisko 2");
        (await l.Org.PutAsJsonAsync($"{l.Path}/schedule", new { pitchId = l.S.PitchId, scheduledAt = later })).IsSuccessStatusCode.Should().BeFalse();
        (await l.Org.PatchAsJsonAsync($"{l.Path}/pitch", new { pitchId = pitch2 })).IsSuccessStatusCode.Should().BeFalse();
        (await l.Org.PatchAsJsonAsync($"{l.Path}/referee", new { refereeId = (Guid?)null })).IsSuccessStatusCode.Should().BeFalse();
    }

    [Fact]
    public async Task MNZ_029_ReleasedMatch_ReturnsToTheOpenPool()
    {
        var l = await LiveAsync(start: false);
        var (first, firstId) = await l.S.JoinAsync("Referee");
        var (second, _) = await l.S.JoinAsync("Referee");
        await l.S.AssignRefereeAsync(l.Match, firstId);

        (await first.PostAsync($"/api/tournaments/{l.S.TournamentId}/matches/{l.Match}/referee/release", null)).IsSuccessStatusCode.Should().BeTrue();

        (await (await first.GetAsync("/api/referee/matches")).ReadJsonAsync()).EnumerateArray().Should().BeEmpty();
        (await (await second.GetAsync("/api/referee/matches/open")).ReadJsonAsync()).EnumerateArray()
            .Should().Contain(m => m.GetGuid("id") == l.Match);
    }

    [Fact]
    public async Task MNZ_030_RefereeCannotClaimAMatchAtTheSameTimeAsTheirOwn()
    {
        var l = await LiveAsync(start: false);
        var (referee, refereeId) = await l.S.JoinAsync("Referee");
        await l.S.AssignRefereeAsync(l.Match, refereeId);
        var own = (await l.S.ScheduleAsync()).Single(m => m.GetGuid("id") == l.Match);
        var other = (await l.S.MatchBetweenAsync("T3", "T4")).GetGuid("id");
        var pitch2 = await TournamentFlow.AddPitchAsync(l.Org, l.S.TournamentId, "Boisko 2");
        (await l.Org.PutAsJsonAsync($"/api/tournaments/{l.S.TournamentId}/matches/{other}/schedule",
                new { pitchId = pitch2, scheduledAt = DateTimeOffset.Parse(own.GetString("scheduledAt")) }))
            .IsSuccessStatusCode.Should().BeTrue();

        var claim = await referee.PostAsync($"/api/tournaments/{l.S.TournamentId}/matches/{other}/referee/claim", null);

        claim.IsSuccessStatusCode.Should().BeFalse();
    }

    [Fact]
    public async Task MNZ_032a_MNZ_032b_ResultWithoutClock_AcceptsUpTo99Goals()
    {
        var l = await LiveAsync(start: false);
        var other = (await l.S.MatchBetweenAsync("T3", "T4")).GetGuid("id");

        (await l.Org.PostAsJsonAsync($"/api/tournaments/{l.S.TournamentId}/matches/{other}/record-result",
                new { homeScore = 100, awayScore = 0, resultType = Regular, winnerTournamentTeamId = (Guid?)null }))
            .IsSuccessStatusCode.Should().BeFalse();
        (await l.S.MatchBetweenAsync("T3", "T4")).GetString("status").Should().Be("Scheduled");

        var record = await l.Org.PostAsJsonAsync($"{l.Path}/record-result", new { homeScore = 99, awayScore = 0, resultType = Regular, winnerTournamentTeamId = (Guid?)null });
        record.IsSuccessStatusCode.Should().BeTrue(await record.Content.ReadAsStringAsync());

        (await ScoreAsync(l)).Should().Be((99, 0));
        (await EventsAsync(l)).Count(e => e.GetString("type") == "Goal" && e.GetGuidOrNull("playerId") == null).Should().Be(99);
    }

    [Fact]
    public async Task MNZ_033b_OwnGoal_CannotHaveAnAssist()
    {
        var l = await LiveAsync();
        var ownGoal = await EventIdAsync(await GoalAsync(l, l.T1, l.Adam, type: OwnGoal));

        (await l.Org.PatchAsJsonAsync($"{l.Path}/events/{ownGoal}/players", new { playerId = l.Adam, assistPlayerId = l.Bartek }))
            .IsSuccessStatusCode.Should().BeFalse();
    }

    [Fact]
    public async Task MNZ_034_ChangingTheResult_AddsOrRemovesOnlyScorerlessGoals()
    {
        var l = await LiveAsync();
        var home = (await l.S.ScheduleAsync()).Single(m => m.GetGuid("id") == l.Match).GetGuid("homeTournamentTeamId");
        var homePlayer = home == l.T1 ? l.Adam : l.Cezary;
        var scored = await EventIdAsync(await GoalAsync(l, home, homePlayer));
        (await l.Org.PostAsJsonAsync($"{l.Path}/finish", new { resultType = Regular, winnerTournamentTeamId = home })).IsSuccessStatusCode.Should().BeTrue();

        (await l.Org.PatchAsJsonAsync($"{l.Path}/result", new { resultType = Regular, winnerTournamentTeamId = home, homeScore = 3, awayScore = 1 }))
            .IsSuccessStatusCode.Should().BeTrue();
        (await ScoreAsync(l)).Should().Be((3, 1));
        (await EventsAsync(l)).Count(e => e.GetString("type") == "Goal" && e.GetGuidOrNull("playerId") == null).Should().Be(3);

        (await l.Org.PatchAsJsonAsync($"{l.Path}/result", new { resultType = Regular, winnerTournamentTeamId = home, homeScore = 2, awayScore = 1 }))
            .IsSuccessStatusCode.Should().BeTrue();
        (await ScoreAsync(l)).Should().Be((2, 1));
        var goals = (await EventsAsync(l)).Where(e => e.GetString("type") == "Goal").ToList();
        goals.Should().Contain(e => e.GetGuid("id") == scored);
        goals.Should().HaveCount(3);
    }
}

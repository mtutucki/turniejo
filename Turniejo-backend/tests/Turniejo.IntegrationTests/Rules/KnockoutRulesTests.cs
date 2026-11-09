using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Turniejo.IntegrationTests.Infrastructure;
using Turniejo.Tournaments.Domain.Enums;

namespace Turniejo.IntegrationTests.Rules;

/// <summary>
/// Arkusz QA „Puchar”: reguły, które w ręcznym przebiegu wyszły OK. Puchar z pomocnika ma pary
/// T1-T2, T3-T4… w 1. rundzie; terminarz na jednym boisku, mecz 30 min + 5 min przerwy.
/// </summary>
public sealed class KnockoutRulesTests(IntegrationTestWebApplicationFactory factory) : IntegrationTestBase(factory)
{
    private const int Fixed = 1, Bye = 5;
    private const int Regular = 2, ExtraTime = 3, Penalties = 4, Walkover = 5;

    private static string T(TournamentScenario s) => $"/api/tournaments/{s.TournamentId}";
    private static string M(TournamentScenario s, Guid matchId) => $"{T(s)}/matches/{matchId}";

    private static async Task<List<JsonElement>> RoundsAsync(TournamentScenario s, Guid? categoryId = null)
    {
        var response = await s.Organizer.GetAsync($"{T(s)}/bracket?categoryId={categoryId ?? s.CategoryId}");
        if (!response.IsSuccessStatusCode)
            return [];
        return (await response.ReadJsonAsync()).GetProperty("rounds").EnumerateArray().OrderBy(r => r.GetInt("roundNumber")).ToList();
    }

    private static async Task<List<JsonElement>> NodesAsync(TournamentScenario s, int round, string type = "Standard") =>
        (await RoundsAsync(s)).Single(r => r.GetInt("roundNumber") == round && r.GetString("roundType") == type)
            .GetProperty("nodes").EnumerateArray().ToList();

    private static IEnumerable<Guid?> TeamsOf(JsonElement node) =>
        [node.GetGuidOrNull("homeTournamentTeamId"), node.GetGuidOrNull("awayTournamentTeamId")];

    private static async Task GenerateBracketAsync(TournamentScenario s, params (int HomeType, Guid? Home, int AwayType, Guid? Away)[] pairs)
    {
        var response = await s.Organizer.PostAsJsonAsync($"{T(s)}/bracket/generate", new
        {
            categoryId = s.CategoryId,
            roundOnePairings = pairs.Select(p => new
            {
                homeSourceType = p.HomeType, homeGroupId = (Guid?)null, homePosition = (int?)null, homeTournamentTeamId = p.Home,
                awaySourceType = p.AwayType, awayGroupId = (Guid?)null, awayPosition = (int?)null, awayTournamentTeamId = p.Away,
            }),
        });
        response.IsSuccessStatusCode.Should().BeTrue(await response.Content.ReadAsStringAsync());
    }

    private static async Task<JsonElement> MatchAsync(TournamentScenario s, Guid matchId) =>
        (await s.ScheduleAsync()).Single(m => m.GetGuid("id") == matchId);

    private static async Task<Guid> MatchIdAsync(TournamentScenario s, string home, string away) =>
        (await s.MatchBetweenAsync(home, away)).GetGuid("id");

    private static async Task StartAsync(TournamentScenario s, Guid matchId, HttpClient? actor = null) =>
        (await (actor ?? s.Organizer).PostAsync($"{M(s, matchId)}/start", null)).IsSuccessStatusCode.Should().BeTrue();

    private static async Task GoalAsync(TournamentScenario s, Guid matchId, string team, HttpClient? actor = null) =>
        await TournamentFlow.AddGoalAsync(actor ?? s.Organizer, s.TournamentId, matchId, s.Team(team), 10);

    private static Task<HttpResponseMessage> FinishAsync(TournamentScenario s, Guid matchId, int resultType, Guid? winner, int? homePens = null, int? awayPens = null, HttpClient? actor = null) =>
        (actor ?? s.Organizer).PostAsJsonAsync($"{M(s, matchId)}/finish",
            new { resultType, winnerTournamentTeamId = winner, homePenaltyScore = homePens, awayPenaltyScore = awayPens });

    /// <summary>Wygrana gospodarza <paramref name="winner"/> 1:0 (albo gościa, jeśli to gość).</summary>
    private static async Task WinAsync(TournamentScenario s, string home, string away, string winner)
    {
        var match = await MatchIdAsync(s, home, away);
        await s.PlayAsync(match, (await MatchAsync(s, match)).GetGuid("homeTournamentTeamId") == s.Team(winner) ? 1 : 0,
            (await MatchAsync(s, match)).GetGuid("awayTournamentTeamId") == s.Team(winner) ? 1 : 0);
    }

    private static async Task<string> StatusAsync(TournamentScenario s) => (await s.TournamentAsync()).GetString("status");

    [Fact]
    public async Task PUC_005_PUC_007_EightAcceptedTeams_AndClosedRegistration()
    {
        var s = await TournamentScenario.KnockoutAsync(Factory, teams: 8, schedule: false, bracket: false);

        (await StatusAsync(s)).Should().Be("RegistrationClosed");
        var counts = (await (await s.Organizer.GetAsync($"{T(s)}/dashboard")).ReadJsonAsync()).GetProperty("teams");
        counts.GetInt("accepted").Should().Be(8);
    }

    [Fact]
    public async Task PUC_010_SwappingRoundOneSlots_ChangesTheMatchesWithoutDuplicates()
    {
        var s = await TournamentScenario.KnockoutAsync(Factory, teams: 4, schedule: false);
        var round1 = await NodesAsync(s, 1);
        var first = round1.Single(n => TeamsOf(n).Contains(s.Team("T1")));
        var second = round1.Single(n => TeamsOf(n).Contains(s.Team("T3")));

        var swap = await s.Organizer.PostAsJsonAsync($"{T(s)}/bracket/swap-slots",
            new { categoryId = s.CategoryId, firstNodeId = first.GetGuid("id"), firstSlot = 2, secondNodeId = second.GetGuid("id"), secondSlot = 1 });
        swap.IsSuccessStatusCode.Should().BeTrue(await swap.Content.ReadAsStringAsync());
        await s.GenerateScheduleAsync();

        var matches = await s.ScheduleAsync();
        matches.Should().HaveCount(2);
        await s.MatchBetweenAsync("T1", "T3");
        await s.MatchBetweenAsync("T2", "T4");
    }

    [Fact]
    public async Task PUC_016_SwappingTheByeTeam_ChangesWhoAdvancesAndWhoPlays()
    {
        var s = await TournamentScenario.KnockoutAsync(Factory, teams: 3, schedule: false, bracket: false);
        await GenerateBracketAsync(s, (Fixed, s.Team("T1"), Bye, null), (Fixed, s.Team("T2"), Fixed, s.Team("T3")));
        var round1 = await NodesAsync(s, 1);
        var byeNode = round1.Single(n => TeamsOf(n).Contains(s.Team("T1")));
        var playedNode = round1.Single(n => TeamsOf(n).Contains(s.Team("T2")));

        (await s.Organizer.PostAsJsonAsync($"{T(s)}/bracket/swap-slots",
                new { categoryId = s.CategoryId, firstNodeId = byeNode.GetGuid("id"), firstSlot = 1, secondNodeId = playedNode.GetGuid("id"), secondSlot = 1 }))
            .IsSuccessStatusCode.Should().BeTrue();

        TeamsOf((await NodesAsync(s, 2)).Single()).Should().Contain(s.Team("T2")).And.NotContain(s.Team("T1"));
        await s.MatchBetweenAsync("T1", "T3");
        (await s.ScheduleAsync()).Should().ContainSingle();
    }

    [Fact]
    public async Task PUC_018_TwoTeams_GiveOnlyAFinal_EvenWithThirdPlaceEnabled()
    {
        var s = await TournamentScenario.KnockoutAsync(Factory, teams: 2, thirdPlace: true);

        (await RoundsAsync(s)).Should().ContainSingle().Which.GetString("roundType").Should().Be("Standard");
        (await s.ScheduleAsync()).Should().ContainSingle().Which.GetString("stageType").Should().Be("Final");
    }

    [Fact]
    public async Task PUC_021_ThirdPlaceSetting_AppliesOnlyToANewBracket()
    {
        var s = await TournamentScenario.KnockoutAsync(Factory, teams: 4, schedule: false);
        await s.UpdateSettingsAsync(thirdPlace: true);

        (await RoundsAsync(s)).Should().NotContain(r => r.GetString("roundType") == "ThirdPlace");

        (await s.Organizer.DeleteAsync($"{T(s)}/bracket?categoryId={s.CategoryId}")).IsSuccessStatusCode.Should().BeTrue();
        await GenerateBracketAsync(s, (Fixed, s.Team("T1"), Fixed, s.Team("T2")), (Fixed, s.Team("T3"), Fixed, s.Team("T4")));
        (await RoundsAsync(s)).Should().Contain(r => r.GetString("roundType") == "ThirdPlace");
    }

    [Fact]
    public async Task PUC_022_ScheduleWithoutABracket_IsRejected()
    {
        var s = await TournamentScenario.KnockoutAsync(Factory, teams: 4, schedule: false, bracket: false);

        var response = await s.Organizer.PostAsJsonAsync($"{T(s)}/schedule/generate",
            new { startAt = s.StartsAt, endAt = s.StartsAt.AddHours(12), pitchIds = new[] { s.PitchId } });

        response.IsSuccessStatusCode.Should().BeFalse();
        (await StatusAsync(s)).Should().Be("RegistrationClosed");
    }

    [Fact]
    public async Task PUC_024_MatchesAreSpacedByDurationAndBreak_AndLaterRoundsWaitForTheirSources()
    {
        var s = await TournamentScenario.KnockoutAsync(Factory, teams: 8, start: false);

        var times = (await s.ScheduleAsync()).Select(m => DateTimeOffset.Parse(m.GetString("scheduledAt"))).OrderBy(t => t).ToList();
        times.Should().HaveCount(4);
        times.Zip(times.Skip(1), (a, b) => b - a).Should().OnlyContain(gap => gap >= TimeSpan.FromMinutes(35));

        var lastQuarterFinalEnd = times.Max().AddMinutes(30);
        var semiFinals = await NodesAsync(s, 2);
        semiFinals.Should().OnlyContain(n => DateTimeOffset.Parse(n.GetProperty("plannedAt").GetString()!) >= times.Min().AddMinutes(30));
        var final = (await NodesAsync(s, 3)).Single();
        DateTimeOffset.Parse(final.GetProperty("plannedAt").GetString()!).Should().BeOnOrAfter(lastQuarterFinalEnd);
    }

    [Fact]
    public async Task PUC_028_PUC_029_ClearingTheSchedule_KeepsBracketMatches_AndRegeneratingDoesNotDuplicate()
    {
        var s = await TournamentScenario.KnockoutAsync(Factory, teams: 4, start: false);
        (await StatusAsync(s)).Should().Be("ScheduleGenerated");

        (await s.Organizer.DeleteAsync($"{T(s)}/schedule")).IsSuccessStatusCode.Should().BeTrue();

        (await StatusAsync(s)).Should().Be("RegistrationClosed");
        (await s.ScheduleAsync()).Should().HaveCount(2);
        (await NodesAsync(s, 2)).Single().GetProperty("plannedAt").ValueKind.Should().Be(JsonValueKind.Null);

        var other = await TournamentFlow.AddPitchAsync(s.Organizer, s.TournamentId, "Boisko 2");
        var regenerate = await s.Organizer.PostAsJsonAsync($"{T(s)}/schedule/generate",
            new { startAt = s.StartsAt.AddHours(2), endAt = s.StartsAt.AddHours(10), pitchIds = new[] { other } });
        regenerate.IsSuccessStatusCode.Should().BeTrue(await regenerate.Content.ReadAsStringAsync());
        (await s.ScheduleAsync()).Should().HaveCount(2).And.OnlyContain(m => m.GetGuid("pitchId") == other);
    }

    [Fact]
    public async Task PUC_030_ResettingTheBracket_RemovesItsMatches_AndAllowsStartingOver()
    {
        var s = await TournamentScenario.KnockoutAsync(Factory, teams: 4, start: false);

        (await s.Organizer.DeleteAsync($"{T(s)}/bracket?categoryId={s.CategoryId}")).IsSuccessStatusCode.Should().BeTrue();

        (await StatusAsync(s)).Should().Be("RegistrationClosed");
        (await s.ScheduleAsync()).Should().BeEmpty();
        await GenerateBracketAsync(s, (Fixed, s.Team("T1"), Fixed, s.Team("T4")), (Fixed, s.Team("T2"), Fixed, s.Team("T3")));
        await s.GenerateScheduleAsync();
        (await s.ScheduleAsync()).Should().HaveCount(2);
    }

    [Fact]
    public async Task PUC_034_RefereeBusyAtTheSameTime_CannotBeAssigned()
    {
        var s = await TournamentScenario.KnockoutAsync(Factory, teams: 4, start: false);
        var (_, refereeId) = await s.JoinAsync("Referee");
        var first = (await s.MatchBetweenAsync("T1", "T2"));
        var second = await MatchIdAsync(s, "T3", "T4");
        var pitch2 = await TournamentFlow.AddPitchAsync(s.Organizer, s.TournamentId, "Boisko 2");
        (await s.Organizer.PutAsJsonAsync($"{M(s, second)}/schedule", new { pitchId = pitch2, scheduledAt = DateTimeOffset.Parse(first.GetString("scheduledAt")) }))
            .IsSuccessStatusCode.Should().BeTrue();

        await s.AssignRefereeAsync(first.GetGuid("id"), refereeId);
        var busy = await s.Organizer.PatchAsJsonAsync($"{M(s, second)}/referee", new { refereeId });

        busy.StatusCode.Should().Be(System.Net.HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task PUC_036_PUC_037_QuarterFinalWinners_FillTheSemiFinal_WhichIsCreatedWithItsPlannedTerm()
    {
        var s = await TournamentScenario.KnockoutAsync(Factory, teams: 8);
        var qf1 = await MatchIdAsync(s, "T1", "T2");
        await StartAsync(s, qf1);
        await GoalAsync(s, qf1, "T1");
        await GoalAsync(s, qf1, "T1");
        await GoalAsync(s, qf1, "T2");
        (await FinishAsync(s, qf1, Regular, s.Team("T1"))).IsSuccessStatusCode.Should().BeTrue();
        await FlushOutboxAsync();
        var semi = (await NodesAsync(s, 2)).Single(n => TeamsOf(n).Contains(s.Team("T1")));
        semi.GetGuidOrNull("matchId").Should().BeNull();
        var planned = DateTimeOffset.Parse(semi.GetProperty("plannedAt").GetString()!);

        await WinAsync(s, "T3", "T4", "T4");
        await FlushOutboxAsync();

        var semiMatch = await s.MatchBetweenAsync("T1", "T4");
        (await s.ScheduleAsync()).Count(m => m.GetGuid("id") == semiMatch.GetGuid("id")).Should().Be(1);
        DateTimeOffset.Parse(semiMatch.GetString("scheduledAt")).Should().Be(planned);
        var (_, refereeId) = await s.JoinAsync("Referee");
        await s.AssignRefereeAsync(semiMatch.GetGuid("id"), refereeId);
    }

    [Fact]
    public async Task PUC_038_DrawInAKnockoutMatch_CannotEndAsRegular_OnlyWithPenalties()
    {
        var s = await TournamentScenario.KnockoutAsync(Factory, teams: 2);
        var final = await MatchIdAsync(s, "T1", "T2");
        await StartAsync(s, final);
        await GoalAsync(s, final, "T1");
        await GoalAsync(s, final, "T2");

        (await FinishAsync(s, final, Regular, null)).IsSuccessStatusCode.Should().BeFalse();
        (await FinishAsync(s, final, Penalties, s.Team("T1"), 4, 3)).IsSuccessStatusCode.Should().BeTrue();
        (await MatchAsync(s, final)).GetString("resultType").Should().Be("Penalties");
    }

    [Fact]
    public async Task PUC_041_ExtraTimeWin_IsMarked_AndTheWinnerAdvances()
    {
        var s = await TournamentScenario.KnockoutAsync(Factory, teams: 4);
        var match = await MatchIdAsync(s, "T1", "T2");
        await StartAsync(s, match);
        await GoalAsync(s, match, "T2");
        await GoalAsync(s, match, "T2");
        await GoalAsync(s, match, "T2");
        await GoalAsync(s, match, "T1");
        await GoalAsync(s, match, "T1");

        (await FinishAsync(s, match, ExtraTime, s.Team("T2"))).IsSuccessStatusCode.Should().BeTrue();
        await FlushOutboxAsync();

        (await MatchAsync(s, match)).GetString("resultType").Should().Be("ExtraTime");
        TeamsOf((await NodesAsync(s, 2)).Single()).Should().Contain(s.Team("T2"));
    }

    [Fact]
    public async Task PUC_042_WalkoverDuringAMatch_GivesTheWinnerTheWalkoverScore_EvenAsAway()
    {
        var s = await TournamentScenario.KnockoutAsync(Factory, teams: 4);
        var match = await MatchIdAsync(s, "T1", "T2");
        await StartAsync(s, match);
        var away = (await MatchAsync(s, match)).GetGuid("awayTournamentTeamId");

        (await FinishAsync(s, match, Walkover, away)).IsSuccessStatusCode.Should().BeTrue();
        await FlushOutboxAsync();

        var finished = await MatchAsync(s, match);
        finished.GetInt("awayScore").Should().Be(3);
        finished.GetInt("homeScore").Should().Be(0);
        TeamsOf((await NodesAsync(s, 2)).Single()).Should().Contain(away);
    }

    [Fact]
    public async Task PUC_044_SemiFinals_SendWinnersToTheFinal_AndLosersToTheThirdPlaceMatch()
    {
        var s = await TournamentScenario.KnockoutAsync(Factory, teams: 4, thirdPlace: true);

        await WinAsync(s, "T1", "T2", "T1");
        await WinAsync(s, "T3", "T4", "T3");
        await FlushOutboxAsync();

        (await s.MatchBetweenAsync("T1", "T3")).GetString("stageType").Should().Be("Final");
        (await s.MatchBetweenAsync("T2", "T4")).GetString("stageType").Should().Be("ThirdPlace");
    }

    [Fact]
    public async Task PUC_047_CorrectionBeforeTheNextMatchExists_PutsTheNewWinnerInTheSlot()
    {
        var s = await TournamentScenario.KnockoutAsync(Factory, teams: 8);
        var match = await MatchIdAsync(s, "T1", "T2");
        await WinAsync(s, "T1", "T2", "T1");
        await FlushOutboxAsync();
        var winnerIsHome = (await MatchAsync(s, match)).GetGuid("homeTournamentTeamId") == s.Team("T1");

        var correct = await s.Organizer.PatchAsJsonAsync($"{M(s, match)}/correct-score",
            new { homeScore = winnerIsHome ? 0 : 2, awayScore = winnerIsHome ? 2 : 0, winnerTournamentTeamId = s.Team("T2") });
        correct.IsSuccessStatusCode.Should().BeTrue(await correct.Content.ReadAsStringAsync());
        await FlushOutboxAsync();

        var semi = (await NodesAsync(s, 2)).Single(n => TeamsOf(n).Contains(s.Team("T2")) || TeamsOf(n).Contains(s.Team("T1")));
        TeamsOf(semi).Should().Contain(s.Team("T2")).And.NotContain(s.Team("T1"));
    }

    [Fact]
    public async Task PUC_048_CorrectionAfterTheNextMatchWasCreated_SwapsTheTeamInIt()
    {
        var s = await TournamentScenario.KnockoutAsync(Factory, teams: 4);
        var match = await MatchIdAsync(s, "T1", "T2");
        await WinAsync(s, "T1", "T2", "T1");
        await WinAsync(s, "T3", "T4", "T3");
        await FlushOutboxAsync();
        var final = (await s.MatchBetweenAsync("T1", "T3")).GetGuid("id");
        var winnerIsHome = (await MatchAsync(s, match)).GetGuid("homeTournamentTeamId") == s.Team("T1");

        (await s.Organizer.PatchAsJsonAsync($"{M(s, match)}/correct-score",
                new { homeScore = winnerIsHome ? 0 : 2, awayScore = winnerIsHome ? 2 : 0, winnerTournamentTeamId = s.Team("T2") }))
            .IsSuccessStatusCode.Should().BeTrue();
        await FlushOutboxAsync();

        var updated = await MatchAsync(s, final);
        new[] { updated.GetGuid("homeTournamentTeamId"), updated.GetGuid("awayTournamentTeamId") }
            .Should().BeEquivalentTo([s.Team("T2"), s.Team("T3")]);
    }

    [Fact]
    public async Task PUC_050_PUC_051_PUC_074_RefereeEditWindowOfZero_BlocksTheReferee_ButNotTheOrganizer()
    {
        var s = await TournamentScenario.KnockoutAsync(Factory, teams: 4, start: false);
        await s.UpdateSettingsAsync(editWindowMinutes: 0);
        await s.StartAsync();
        var (referee, refereeId) = await s.JoinAsync("Referee");
        var match = await MatchIdAsync(s, "T1", "T2");
        await s.AssignRefereeAsync(match, refereeId);
        await StartAsync(s, match, referee);
        await GoalAsync(s, match, "T1", referee);
        var winner = s.Team("T1");
        (await FinishAsync(s, match, Regular, winner, actor: referee)).IsSuccessStatusCode.Should().BeTrue();
        var homeIsT1 = (await MatchAsync(s, match)).GetGuid("homeTournamentTeamId") == winner;

        (await referee.PatchAsJsonAsync($"{M(s, match)}/correct-score", new { homeScore = homeIsT1 ? 2 : 0, awayScore = homeIsT1 ? 0 : 2, winnerTournamentTeamId = winner }))
            .IsSuccessStatusCode.Should().BeFalse();
        (await TournamentFlow.AddGoalAsyncRaw(referee, s.TournamentId, match, winner)).IsSuccessStatusCode.Should().BeFalse();

        var organizerChange = await s.Organizer.PatchAsJsonAsync($"{M(s, match)}/result",
            new { resultType = Regular, winnerTournamentTeamId = s.Team("T2"), homeScore = homeIsT1 ? 0 : 3, awayScore = homeIsT1 ? 3 : 0 });
        organizerChange.IsSuccessStatusCode.Should().BeTrue(await organizerChange.Content.ReadAsStringAsync());
        await FlushOutboxAsync();
        TeamsOf((await NodesAsync(s, 2)).Single()).Should().Contain(s.Team("T2"));
    }

    [Fact]
    public async Task PUC_052_WithdrawingATeamDuringTheTournament_GivesTheRivalAWalkover()
    {
        var s = await TournamentScenario.KnockoutAsync(Factory, teams: 4);
        var match = await MatchIdAsync(s, "T1", "T2");

        (await s.Organizer.PatchAsJsonAsync($"{T(s)}/tournament-teams/{s.Team("T1")}/registration-status", new { registrationStatus = 4 }))
            .IsSuccessStatusCode.Should().BeTrue();
        await FlushOutboxAsync();

        var walkover = await MatchAsync(s, match);
        walkover.GetString("status").Should().Be("Finished");
        walkover.GetString("resultType").Should().Be("Walkover");
        TeamsOf((await NodesAsync(s, 2)).Single()).Should().Contain(s.Team("T2"));
    }

    [Fact]
    public async Task PUC_053_WithdrawingATeamBeforeTheStart_CancelsItsMatch()
    {
        var s = await TournamentScenario.KnockoutAsync(Factory, teams: 4, start: false);
        var match = await MatchIdAsync(s, "T1", "T2");

        (await s.Organizer.PatchAsJsonAsync($"{T(s)}/tournament-teams/{s.Team("T1")}/registration-status", new { registrationStatus = 4 }))
            .IsSuccessStatusCode.Should().BeTrue();
        await FlushOutboxAsync();

        (await MatchAsync(s, match)).GetString("status").Should().Be("Cancelled");
        (await s.Organizer.DeleteAsync($"{T(s)}/bracket?categoryId={s.CategoryId}")).IsSuccessStatusCode.Should().BeTrue();
    }

    [Fact]
    public async Task PUC_055_PUC_056_AfterAMatchStarted_TheBracketCannotBeResetOrSwapped()
    {
        var s = await TournamentScenario.KnockoutAsync(Factory, teams: 4);
        await StartAsync(s, await MatchIdAsync(s, "T1", "T2"));
        var round1 = await NodesAsync(s, 1);

        (await s.Organizer.DeleteAsync($"{T(s)}/bracket?categoryId={s.CategoryId}")).IsSuccessStatusCode.Should().BeFalse();
        (await s.Organizer.PostAsJsonAsync($"{T(s)}/bracket/swap-slots",
                new { categoryId = s.CategoryId, firstNodeId = round1[0].GetGuid("id"), firstSlot = 1, secondNodeId = round1[1].GetGuid("id"), secondSlot = 1 }))
            .IsSuccessStatusCode.Should().BeFalse();
    }

    [Fact]
    public async Task PUC_057_TournamentCannotBeFinishedWithAMatchInProgress()
    {
        var s = await TournamentScenario.KnockoutAsync(Factory, teams: 2);
        await StartAsync(s, await MatchIdAsync(s, "T1", "T2"));

        var finish = await s.Organizer.PatchAsJsonAsync($"{T(s)}/status", new { tournamentStatus = (int)TournamentStatus.Finished });

        finish.IsSuccessStatusCode.Should().BeFalse();
        (await StatusAsync(s)).Should().Be("InProgress");
    }

    [Fact]
    public async Task PUC_059_CancellingDuringTheTournament_CancelsMatches_AndNotifiesParticipantsButNotTheCanceller()
    {
        var s = await TournamentScenario.KnockoutAsync(Factory, teams: 4);
        var (coach, coachId) = await s.JoinAsync("Coach");
        await s.AssignCoachAsync("T1", coachId);
        var (referee, refereeId) = await s.JoinAsync("Referee");
        await s.AssignRefereeAsync(await MatchIdAsync(s, "T3", "T4"), refereeId);

        await TournamentFlow.ChangeStatusAsync(s.Organizer, s.TournamentId, TournamentStatus.Cancelled);
        await FlushOutboxAsync();

        (await s.ScheduleAsync()).Should().OnlyContain(m => m.GetString("status") == "Cancelled");
        (await s.NotificationsAsync(coach)).Should().Contain(n => n.GetString("type") == "TournamentCancelled");
        (await s.NotificationsAsync(referee)).Should().Contain(n => n.GetString("type") == "TournamentCancelled");
        (await s.NotificationsAsync(s.Organizer)).Should().NotContain(n => n.GetString("type") == "TournamentCancelled");
        (await s.Organizer.DeleteAsync(T(s))).IsSuccessStatusCode.Should().BeTrue();
    }

    [Fact]
    public async Task PUC_060_FormatCannotChangeAfterTheScheduleIsGenerated()
    {
        var s = await TournamentScenario.KnockoutAsync(Factory, teams: 4, start: false);

        var change = await s.Organizer.PatchAsJsonAsync($"{T(s)}/format", new { tournamentFormat = (int)TournamentFormat.Group });

        change.IsSuccessStatusCode.Should().BeFalse();
    }

    [Fact]
    public async Task PUC_061_TwoCategories_HaveSeparateBrackets_AndTheScheduleHasNoPitchCollisions()
    {
        var s = await TournamentScenario.KnockoutAsync(Factory, teams: 4, schedule: false, bracket: false);
        await TournamentFlow.ChangeStatusAsync(s.Organizer, s.TournamentId, TournamentStatus.RegistrationOpen);
        var u10 = await TournamentFlow.AddCategoryAsync(s.Organizer, s.TournamentId, "U10");
        foreach (var name in new[] { "M1", "M2", "M3", "M4" })
            await s.AddAcceptedTeamAsync(name, u10);
        await s.CloseRegistrationAsync();
        await GenerateBracketAsync(s, (Fixed, s.Team("T1"), Fixed, s.Team("T2")), (Fixed, s.Team("T3"), Fixed, s.Team("T4")));
        (await s.Organizer.PostAsJsonAsync($"{T(s)}/bracket/generate", new
        {
            categoryId = u10,
            roundOnePairings = new[] { ("M1", "M2"), ("M3", "M4") }.Select(p => new
            {
                homeSourceType = Fixed, homeGroupId = (Guid?)null, homePosition = (int?)null, homeTournamentTeamId = (Guid?)s.Team(p.Item1),
                awaySourceType = Fixed, awayGroupId = (Guid?)null, awayPosition = (int?)null, awayTournamentTeamId = (Guid?)s.Team(p.Item2),
            }),
        })).IsSuccessStatusCode.Should().BeTrue();

        await s.GenerateScheduleAsync();

        var matches = await s.ScheduleAsync();
        matches.Should().HaveCount(4);
        var u10Teams = new[] { "M1", "M2", "M3", "M4" }.Select(s.Team).ToHashSet();
        matches.Where(m => m.GetGuidOrNull("categoryId") == u10)
            .Should().HaveCount(2).And.OnlyContain(m => u10Teams.Contains(m.GetGuid("homeTournamentTeamId")) && u10Teams.Contains(m.GetGuid("awayTournamentTeamId")));
        matches.GroupBy(m => (m.GetGuid("pitchId"), m.GetString("scheduledAt"))).Should().OnlyContain(g => g.Count() == 1);
        (await RoundsAsync(s, u10)).Should().NotBeEmpty();
    }

    [Fact]
    public async Task PUC_067_DailyMatchLimit_IsRespected()
    {
        var s = await TournamentScenario.KnockoutAsync(Factory, teams: 8, schedule: false);
        // Turniej trwa dobę: dwa „dni” to dwa okna po kilka godzin.
        var day2 = s.StartsAt.AddHours(12);

        var response = await s.Organizer.PostAsJsonAsync($"{T(s)}/schedule/generate", new
        {
            startAt = s.StartsAt, endAt = day2.AddHours(10), pitchIds = new[] { s.PitchId },
            windows = new[]
            {
                new { startAt = s.StartsAt, endAt = s.StartsAt.AddHours(8), maxMatches = (int?)3 },
                new { startAt = day2, endAt = day2.AddHours(10), maxMatches = (int?)null },
            },
        });
        response.IsSuccessStatusCode.Should().BeTrue(await response.Content.ReadAsStringAsync());

        var times = (await s.ScheduleAsync()).Select(m => DateTimeOffset.Parse(m.GetString("scheduledAt"))).ToList();
        var split = day2.AddMinutes(-1); // baza trzyma czas z dokładnością do mikrosekund
        times.Count(t => t < split).Should().Be(3);
        times.Count(t => t >= split).Should().Be(1);
    }

    [Fact]
    public async Task PUC_070a_PUC_070b_FutureRoundTermAndReferee_MustComeAfterItsSourceMatches()
    {
        var s = await TournamentScenario.KnockoutAsync(Factory, teams: 4, start: false);
        var (_, refereeId) = await s.JoinAsync("Referee");
        var final = (await NodesAsync(s, 2)).Single();
        var latestSource = (await s.ScheduleAsync()).Max(m => DateTimeOffset.Parse(m.GetString("scheduledAt")));
        var path = $"{T(s)}/bracket-nodes/{final.GetGuid("id")}";

        (await s.Organizer.PutAsJsonAsync($"{path}/schedule", new { pitchId = s.PitchId, scheduledAt = latestSource }))
            .IsSuccessStatusCode.Should().BeFalse();

        var later = latestSource.AddHours(2);
        (await s.Organizer.PutAsJsonAsync($"{path}/schedule", new { pitchId = s.PitchId, scheduledAt = later }))
            .IsSuccessStatusCode.Should().BeTrue();
        (await s.Organizer.PatchAsJsonAsync($"{path}/referee", new { refereeId })).IsSuccessStatusCode.Should().BeTrue();

        var updated = (await NodesAsync(s, 2)).Single();
        DateTimeOffset.Parse(updated.GetProperty("plannedAt").GetString()!).Should().Be(later);
        updated.GetGuidOrNull("plannedRefereeId").Should().Be(refereeId);
    }

    [Fact]
    public async Task PUC_072_ResultWithoutClock_WithPenalties_SendsTheShootoutWinnerOn()
    {
        var s = await TournamentScenario.KnockoutAsync(Factory, teams: 4);
        var match = await MatchIdAsync(s, "T1", "T2");
        var home = (await MatchAsync(s, match)).GetGuid("homeTournamentTeamId");

        (await s.Organizer.PostAsJsonAsync($"{M(s, match)}/record-result",
                new { homeScore = 1, awayScore = 1, resultType = Regular, winnerTournamentTeamId = (Guid?)null }))
            .IsSuccessStatusCode.Should().BeFalse();
        var record = await s.Organizer.PostAsJsonAsync($"{M(s, match)}/record-result",
            new { homeScore = 1, awayScore = 1, resultType = Penalties, winnerTournamentTeamId = home, homePenaltyScore = 5, awayPenaltyScore = 4 });
        record.IsSuccessStatusCode.Should().BeTrue(await record.Content.ReadAsStringAsync());
        await FlushOutboxAsync();

        var finished = await MatchAsync(s, match);
        finished.GetString("status").Should().Be("Finished");
        finished.GetInt("homePenaltyScore").Should().Be(5);
        TeamsOf((await NodesAsync(s, 2)).Single()).Should().Contain(home);
    }

    [Fact]
    public async Task PUC_073_ChangingTheResultToAWalkoverForTheGuest_MovesTheGuestOn()
    {
        var s = await TournamentScenario.KnockoutAsync(Factory, teams: 4);
        var match = await MatchIdAsync(s, "T1", "T2");
        var played = await MatchAsync(s, match);
        var (home, away) = (played.GetGuid("homeTournamentTeamId"), played.GetGuid("awayTournamentTeamId"));
        await s.PlayAsync(match, 2, 0);

        var change = await s.Organizer.PatchAsJsonAsync($"{M(s, match)}/result", new { resultType = Walkover, winnerTournamentTeamId = away });
        change.IsSuccessStatusCode.Should().BeTrue(await change.Content.ReadAsStringAsync());
        await FlushOutboxAsync();

        var updated = await MatchAsync(s, match);
        (updated.GetInt("homeScore"), updated.GetInt("awayScore")).Should().Be((0, 3));
        TeamsOf((await NodesAsync(s, 2)).Single()).Should().Contain(away).And.NotContain(home);
    }
}

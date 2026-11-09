using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Turniejo.IntegrationTests.Infrastructure;
using Turniejo.Tournaments.Domain.Enums;

namespace Turniejo.IntegrationTests.Rules;

/// <summary>
/// Arkusz QA „Grupy+Puchar”: reguły, które w ręcznym przebiegu wyszły OK. Dwie grupy po 3 drużyny
/// (A: T1-T3, B: T4-T6), półfinały na krzyż: 1A-2B i 1B-2A.
/// </summary>
public sealed class GroupAndKnockoutRulesTests(IntegrationTestWebApplicationFactory factory) : IntegrationTestBase(factory)
{
    private const int GroupPosition = 2;

    private static string T(TournamentScenario s) => $"/api/tournaments/{s.TournamentId}";

    private static async Task<TournamentScenario> CrossBracketAsync(IntegrationTestWebApplicationFactory factory, bool start = true)
    {
        var s = await TournamentScenario.GroupsAsync(factory, groups: 2, teamsPerGroup: 3, start: start, format: TournamentFormat.GroupAndKnockout);
        object Pair(string homeGroup, int homePos, string awayGroup, int awayPos) => new
        {
            homeSourceType = GroupPosition, homeGroupId = (Guid?)s.Group(homeGroup), homePosition = (int?)homePos, homeTournamentTeamId = (Guid?)null,
            awaySourceType = GroupPosition, awayGroupId = (Guid?)s.Group(awayGroup), awayPosition = (int?)awayPos, awayTournamentTeamId = (Guid?)null,
        };
        var response = await s.Organizer.PostAsJsonAsync($"{T(s)}/bracket/generate",
            new { categoryId = s.CategoryId, roundOnePairings = new[] { Pair("A", 1, "B", 2), Pair("B", 1, "A", 2) } });
        response.IsSuccessStatusCode.Should().BeTrue(await response.Content.ReadAsStringAsync());
        return s;
    }

    private static async Task<List<JsonElement>> SemiFinalsAsync(TournamentScenario s) =>
        (await (await s.Organizer.GetAsync($"{T(s)}/bracket?categoryId={s.CategoryId}")).ReadJsonAsync())
            .GetProperty("rounds").EnumerateArray().Single(r => r.GetInt("roundNumber") == 1)
            .GetProperty("nodes").EnumerateArray().ToList();

    /// <summary>Gra całą grupę: niższy numer drużyny wygrywa, więc kolejność w tabeli to T1, T2, T3.</summary>
    private static async Task PlayGroupAsync(TournamentScenario s, params (string Winner, string Loser)[] matches)
    {
        foreach (var (winner, loser) in matches)
        {
            var match = await s.MatchBetweenAsync(winner, loser);
            var winnerIsHome = match.GetGuid("homeTournamentTeamId") == s.Team(winner);
            await s.PlayAsync(match.GetGuid("id"), winnerIsHome ? 1 : 0, winnerIsHome ? 0 : 1);
        }
    }

    private static readonly (string, string)[] GroupA = [("T1", "T2"), ("T1", "T3"), ("T2", "T3")];
    private static readonly (string, string)[] GroupB = [("T4", "T5"), ("T4", "T6"), ("T5", "T6")];

    [Fact]
    public async Task GPK_003_GPK_004_SlotsStayEmpty_UntilTheWholeGroupIsPlayed()
    {
        var s = await CrossBracketAsync(Factory);

        var before = await SemiFinalsAsync(s);
        before.Should().OnlyContain(n => n.GetString("homeSourceType") == "GroupPosition" && n.GetGuidOrNull("homeTournamentTeamId") == null);
        (await s.ScheduleAsync()).Should().OnlyContain(m => m.GetString("stageType") == "Group");

        await PlayGroupAsync(s, GroupA[0], GroupA[1]);

        (await SemiFinalsAsync(s)).Should().OnlyContain(n => n.GetGuidOrNull("homeTournamentTeamId") == null && n.GetGuidOrNull("awayTournamentTeamId") == null);
    }

    [Fact]
    public async Task GPK_005_FinishedGroups_FillTheSlotsFromTheTable_AndCreateKnockoutMatchesAfterTheGroupStage()
    {
        var s = await CrossBracketAsync(Factory);
        var lastGroupMatch = (await s.ScheduleAsync()).Max(m => DateTimeOffset.Parse(m.GetString("scheduledAt")));

        await PlayGroupAsync(s, GroupA);
        await PlayGroupAsync(s, GroupB);
        await FlushOutboxAsync();

        var semis = await SemiFinalsAsync(s);
        semis.Select(n => (n.GetGuidOrNull("homeTournamentTeamId"), n.GetGuidOrNull("awayTournamentTeamId")))
            .Should().BeEquivalentTo([((Guid?)s.Team("T1"), (Guid?)s.Team("T5")), (s.Team("T4"), s.Team("T2"))]);
        var first = await s.MatchBetweenAsync("T1", "T5");
        var second = await s.MatchBetweenAsync("T4", "T2");
        DateTimeOffset.Parse(first.GetString("scheduledAt")).Should().BeAfter(lastGroupMatch);
        DateTimeOffset.Parse(second.GetString("scheduledAt")).Should().BeAfter(lastGroupMatch);
    }

    [Fact]
    public async Task GPK_009_TeamWithdrawnAfterTheGroupStage_GivesItsKnockoutRivalAWalkover()
    {
        var s = await CrossBracketAsync(Factory);
        await PlayGroupAsync(s, GroupA);
        await PlayGroupAsync(s, GroupB);
        await FlushOutboxAsync();
        var semi = (await s.MatchBetweenAsync("T1", "T5")).GetGuid("id");

        (await s.Organizer.PatchAsJsonAsync($"{T(s)}/tournament-teams/{s.Team("T1")}/registration-status", new { registrationStatus = 4 }))
            .IsSuccessStatusCode.Should().BeTrue();
        await FlushOutboxAsync();

        var walkover = (await s.ScheduleAsync()).Single(m => m.GetGuid("id") == semi);
        walkover.GetString("resultType").Should().Be("Walkover");
        var winnerIsHome = walkover.GetGuid("homeTournamentTeamId") == s.Team("T5");
        (winnerIsHome ? walkover.GetInt("homeScore") > walkover.GetInt("awayScore") : walkover.GetInt("awayScore") > walkover.GetInt("homeScore"))
            .Should().BeTrue("walkower wygrywa rywal wycofanej drużyny");
    }

    [Fact]
    public async Task GPK_010_ClearingTheSchedule_KeepsTheBracketPairs()
    {
        var s = await CrossBracketAsync(Factory, start: false);

        (await s.Organizer.DeleteAsync($"{T(s)}/schedule")).IsSuccessStatusCode.Should().BeTrue();

        (await s.ScheduleAsync()).Should().BeEmpty();
        var semis = await SemiFinalsAsync(s);
        semis.Should().HaveCount(2);
        semis.Select(n => n.GetInt("homeSourcePosition")).Should().BeEquivalentTo([1, 1]);
    }
}

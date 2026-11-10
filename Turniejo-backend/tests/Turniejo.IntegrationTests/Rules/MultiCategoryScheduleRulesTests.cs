using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Turniejo.Api.Controllers;
using Turniejo.Brackets.Domain.Enums;
using Turniejo.IntegrationTests.Infrastructure;
using Turniejo.Tournaments.Domain.Enums;

namespace Turniejo.IntegrationTests.Rules;

/// <summary>
/// Terminarz turnieju z kilkoma kategoriami: jeden terminarz dla wszystkich, nikt nie gra dwóch meczów
/// naraz (także ta sama drużyna w dwóch kategoriach), drużyny mają jak najdłuższe przerwy, a żadna
/// kategoria nie zostaje bez meczów.
/// </summary>
public sealed class MultiCategoryScheduleRulesTests(IntegrationTestWebApplicationFactory factory) : IntegrationTestBase(factory)
{
    private const int MatchMinutes = 30;
    private const int SlotMinutes = 35; // mecz + 5 min przerwy (TournamentScenario.UpdateSettingsAsync)

    private static string T(TournamentScenario s) => $"/api/tournaments/{s.TournamentId}";

    private static async Task<Guid> GroupOfAsync(TournamentScenario s, Guid categoryId) =>
        (await (await s.Organizer.GetAsync($"{T(s)}/groups")).ReadJsonAsync())
            .EnumerateArray().First(g => g.GetGuid("categoryId") == categoryId).GetGuid("id");

    /// <summary>Druga kategoria z drużynami "{name}-1"…; pierwsza może być tą samą drużyną co <paramref name="sharedWith"/>.</summary>
    private static async Task<Guid> AddCategoryAsync(TournamentScenario s, string name, int teams, string? sharedWith = null, bool assignToGroup = false)
    {
        var categoryId = await TournamentFlow.AddCategoryAsync(s.Organizer, s.TournamentId, name);
        var groupId = assignToGroup ? await GroupOfAsync(s, categoryId) : (Guid?)null;
        for (var i = 1; i <= teams; i++)
        {
            var teamName = $"{name}-{i}";
            var ttId = i == 1 && sharedWith != null
                ? await s.AddAcceptedEntryAsync(teamName, s.ClubTeam(sharedWith), categoryId)
                : await s.AddAcceptedTeamAsync(teamName, categoryId);
            if (groupId is { } group)
                await TournamentFlow.AssignTeamToGroupAsync(s.Organizer, s.TournamentId, ttId, group);
        }

        return categoryId;
    }

    private static async Task GenerateBracketAsync(TournamentScenario s, Guid categoryId, params string[] teams)
    {
        var pairings = Enumerable.Range(0, teams.Length / 2)
            .Select(i => new BracketsController.BracketPairingRequest(
                BracketSourceType.Fixed, null, null, s.Team(teams[i * 2]),
                BracketSourceType.Fixed, null, null, s.Team(teams[i * 2 + 1])))
            .ToList();
        var response = await s.Organizer.PostAsJsonAsync($"{T(s)}/bracket/generate",
            new BracketsController.GenerateBracketRequest(categoryId, pairings));
        response.IsSuccessStatusCode.Should().BeTrue(await response.Content.ReadAsStringAsync());
    }

    private static Task<HttpResponseMessage> GenerateAsync(TournamentScenario s, params Guid[] pitchIds) =>
        s.Organizer.PostAsJsonAsync($"{T(s)}/schedule/generate",
            new MatchesController.GenerateScheduleRequest(s.StartsAt, s.StartsAt.AddHours(12), pitchIds));

    private static List<JsonElement> PlannedNodes(JsonElement bracket) =>
        bracket.GetProperty("rounds").EnumerateArray()
            .SelectMany(r => r.GetProperty("nodes").EnumerateArray())
            .Where(n => n.TryGetProperty("plannedAt", out var at) && at.ValueKind == JsonValueKind.String)
            .ToList();

    /// <summary>Mecze każdej drużyny z bazy (klubu) po kolei: początki.</summary>
    private static Dictionary<Guid, List<DateTimeOffset>> StartsByClub(TournamentScenario s, List<JsonElement> matches)
    {
        var clubOf = s.Teams.ToDictionary(t => t.Value, t => s.ClubTeam(t.Key));
        return matches
            .SelectMany(m => new[] { m.GetGuid("homeTournamentTeamId"), m.GetGuid("awayTournamentTeamId") }
                .Select(team => (Club: clubOf[team], At: DateTimeOffset.Parse(m.GetString("scheduledAt")))))
            .GroupBy(x => x.Club)
            .ToDictionary(g => g.Key, g => g.Select(x => x.At).OrderBy(at => at).ToList());
    }

    [Fact]
    public async Task Knockout_CategoryWithoutABracket_BlocksTheWholeSchedule()
    {
        // Dawniej generator rozstawiał tylko gotową drabinkę, etap przechodził na "harmonogram gotowy",
        // a druga kategoria zostawała bez meczów na zawsze.
        var s = await TournamentScenario.KnockoutAsync(Factory, teams: 4, schedule: false);
        await AddCategoryAsync(s, "U12", 4);

        var response = await GenerateAsync(s, s.PitchId);

        response.IsSuccessStatusCode.Should().BeFalse();
        (await response.Content.ReadAsStringAsync()).Should().Contain("Every category needs a knockout bracket before generating the schedule");
        (await s.TournamentAsync()).GetString("status").Should().Be("RegistrationClosed");
    }

    [Fact]
    public async Task Knockout_TwoCategories_GetAllTheirMatches_AndTheSameClubNeverPlaysTwiceAtOnce()
    {
        var s = await TournamentScenario.KnockoutAsync(Factory, teams: 4, schedule: false);
        var u12 = await AddCategoryAsync(s, "U12", 4, sharedWith: "T1");
        await GenerateBracketAsync(s, u12, "U12-1", "U12-2", "U12-3", "U12-4");
        var secondPitch = await TournamentFlow.AddPitchAsync(s.Organizer, s.TournamentId, "Boisko 2");

        var response = await GenerateAsync(s, s.PitchId, secondPitch);

        response.IsSuccessStatusCode.Should().BeTrue(await response.Content.ReadAsStringAsync());
        var matches = await s.ScheduleAsync();
        matches.Should().HaveCount(4, "po dwa półfinały w każdej kategorii");
        StartsByClub(s, matches)[s.ClubTeam("T1")].Should().OnlyHaveUniqueItems();

        // Finały obu kategorii mają termin od razu, po swoich półfinałach. T1 może zagrać w obu finałach,
        // więc nie mogą się odbyć naraz.
        var finals = new List<DateTimeOffset>();
        foreach (var categoryId in new[] { s.CategoryId, u12 })
        {
            var bracket = await (await s.Organizer.GetAsync($"{T(s)}/bracket?categoryId={categoryId}")).ReadJsonAsync();
            var planned = PlannedNodes(bracket);
            planned.Should().ContainSingle();
            var finalAt = DateTimeOffset.Parse(planned.Single().GetString("plannedAt"));
            var lastSemi = matches.Where(m => m.GetGuid("categoryId") == categoryId).Max(m => DateTimeOffset.Parse(m.GetString("scheduledAt")));
            finalAt.Should().BeOnOrAfter(lastSemi.AddMinutes(SlotMinutes));
            finals.Add(finalAt);
        }

        finals.Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public async Task Groups_TwoCategoriesOnTwoPitches_NoTeamPlaysTwoMatchesInARow()
    {
        // 2 × 4 drużyny = 12 meczów na 2 boiskach to 6 terminów. Kategorie grają na zmianę, więc każda
        // drużyna odpoczywa przez jeden mecz, a terminarz nie jest dłuższy.
        var s = await TournamentScenario.GroupsAsync(Factory, groups: 1, teamsPerGroup: 4, schedule: false);
        await s.UpdateSettingsAsync(matchMinutes: MatchMinutes);
        await AddCategoryAsync(s, "U12", 4, assignToGroup: true);
        var secondPitch = await TournamentFlow.AddPitchAsync(s.Organizer, s.TournamentId, "Boisko 2");

        var response = await GenerateAsync(s, s.PitchId, secondPitch);

        response.IsSuccessStatusCode.Should().BeTrue(await response.Content.ReadAsStringAsync());
        var matches = await s.ScheduleAsync();
        matches.Should().HaveCount(12);
        matches.Max(m => DateTimeOffset.Parse(m.GetString("scheduledAt"))).Should().BeCloseTo(s.StartsAt.AddMinutes(5 * SlotMinutes), TimeSpan.FromSeconds(1));
        foreach (var (_, starts) in StartsByClub(s, matches))
            starts.Zip(starts.Skip(1)).Should().OnlyContain(p => p.Second - p.First > TimeSpan.FromMinutes(SlotMinutes));
    }

    [Fact]
    public async Task Groups_SameClubInTwoCategories_NeverPlaysTwoMatchesAtOnce()
    {
        var s = await TournamentScenario.GroupsAsync(Factory, groups: 1, teamsPerGroup: 4, schedule: false);
        await AddCategoryAsync(s, "U12", 4, sharedWith: "T1", assignToGroup: true);
        var secondPitch = await TournamentFlow.AddPitchAsync(s.Organizer, s.TournamentId, "Boisko 2");
        var thirdPitch = await TournamentFlow.AddPitchAsync(s.Organizer, s.TournamentId, "Boisko 3");

        var response = await GenerateAsync(s, s.PitchId, secondPitch, thirdPitch);

        response.IsSuccessStatusCode.Should().BeTrue(await response.Content.ReadAsStringAsync());
        var starts = StartsByClub(s, await s.ScheduleAsync())[s.ClubTeam("T1")];
        starts.Should().HaveCount(6).And.OnlyHaveUniqueItems();
    }

    [Fact]
    public async Task Groups_GroupWithASingleTeam_BlocksTheSchedule()
    {
        // Grupa z jedną drużyną była pomijana po cichu, a ta drużyna nie dostawała żadnego meczu.
        var s = await TournamentScenario.GroupsAsync(Factory, groups: 1, teamsPerGroup: 4, schedule: false);
        var u12 = await TournamentFlow.AddCategoryAsync(s.Organizer, s.TournamentId, "U12");
        var groupId = await GroupOfAsync(s, u12);
        var lone = await s.AddAcceptedTeamAsync("U12-1", u12);
        await TournamentFlow.AssignTeamToGroupAsync(s.Organizer, s.TournamentId, lone, groupId);

        var response = await GenerateAsync(s, s.PitchId);

        response.IsSuccessStatusCode.Should().BeFalse();
        (await s.TournamentAsync()).GetString("status").Should().Be("RegistrationClosed");
    }

    [Fact]
    public async Task GroupAndKnockout_KnockoutMatchesArePlannedRightAway_AfterTheirOwnGroupStage()
    {
        // Mecze pucharu dostają termin przy generowaniu (w dniach gry), a nie dopiero po grupach na
        // "pierwszym wolnym boisku". Puchar kategorii czeka tylko na swoje grupy.
        var s = await TournamentScenario.GroupsAsync(Factory, groups: 1, teamsPerGroup: 4, schedule: false, format: TournamentFormat.GroupAndKnockout);
        var u12 = await AddCategoryAsync(s, "U12", 4, assignToGroup: true);
        foreach (var categoryId in new[] { s.CategoryId, u12 })
            await GenerateGroupBracketAsync(s, categoryId);

        var response = await GenerateAsync(s, s.PitchId);

        response.IsSuccessStatusCode.Should().BeTrue(await response.Content.ReadAsStringAsync());
        var matches = await s.ScheduleAsync();
        matches.Should().HaveCount(12);
        foreach (var categoryId in new[] { s.CategoryId, u12 })
        {
            var bracket = await (await s.Organizer.GetAsync($"{T(s)}/bracket?categoryId={categoryId}")).ReadJsonAsync();
            var finalAt = DateTimeOffset.Parse(PlannedNodes(bracket).Single().GetString("plannedAt"));
            var groupEnd = matches.Where(m => m.GetGuid("categoryId") == categoryId).Max(m => DateTimeOffset.Parse(m.GetString("scheduledAt")));
            finalAt.Should().BeOnOrAfter(groupEnd.AddMinutes(SlotMinutes));
            finalAt.Should().BeBefore(s.StartsAt.AddHours(12));
        }
    }

    [Fact]
    public async Task GroupAndKnockout_ClearingAndRegenerating_PlansTheBracketAgain()
    {
        var s = await TournamentScenario.GroupsAsync(Factory, groups: 1, teamsPerGroup: 4, schedule: false, format: TournamentFormat.GroupAndKnockout);
        await GenerateGroupBracketAsync(s, s.CategoryId);
        (await GenerateAsync(s, s.PitchId)).IsSuccessStatusCode.Should().BeTrue();

        (await s.Organizer.DeleteAsync($"{T(s)}/schedule")).IsSuccessStatusCode.Should().BeTrue();
        var cleared = await (await s.Organizer.GetAsync($"{T(s)}/bracket?categoryId={s.CategoryId}")).ReadJsonAsync();
        PlannedNodes(cleared).Should().BeEmpty();

        var again = await GenerateAsync(s, s.PitchId);

        again.IsSuccessStatusCode.Should().BeTrue(await again.Content.ReadAsStringAsync());
        (await s.ScheduleAsync()).Should().HaveCount(6);
        var bracket = await (await s.Organizer.GetAsync($"{T(s)}/bracket?categoryId={s.CategoryId}")).ReadJsonAsync();
        PlannedNodes(bracket).Should().ContainSingle();
    }

    [Fact]
    public async Task GroupAndKnockout_CategoryWithoutABracket_BlocksTheWholeSchedule()
    {
        var s = await TournamentScenario.GroupsAsync(Factory, groups: 1, teamsPerGroup: 4, schedule: false, format: TournamentFormat.GroupAndKnockout);
        await GenerateGroupBracketAsync(s, s.CategoryId);
        await AddCategoryAsync(s, "U12", 4, assignToGroup: true);

        var response = await GenerateAsync(s, s.PitchId);

        response.IsSuccessStatusCode.Should().BeFalse();
        (await response.Content.ReadAsStringAsync()).Should().Contain("Every category needs a knockout bracket before generating the schedule");
    }

    /// <summary>Finał z dwóch pierwszych miejsc jedynej grupy kategorii.</summary>
    private static async Task GenerateGroupBracketAsync(TournamentScenario s, Guid categoryId)
    {
        var groupId = await GroupOfAsync(s, categoryId);
        var response = await s.Organizer.PostAsJsonAsync($"{T(s)}/bracket/generate",
            new BracketsController.GenerateBracketRequest(categoryId,
            [
                new BracketsController.BracketPairingRequest(BracketSourceType.GroupPosition, groupId, 1, null, BracketSourceType.GroupPosition, groupId, 2, null),
            ]));
        response.IsSuccessStatusCode.Should().BeTrue(await response.Content.ReadAsStringAsync());
    }
}

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Turniejo.Api.Controllers;
using Turniejo.IntegrationTests.Infrastructure;
using Turniejo.Tournaments.Domain.Enums;

namespace Turniejo.IntegrationTests.Rules;

/// <summary>
/// Format „Dwie fazy grupowe”: grupy fazy 1 z przydzielonymi drużynami, grupy fazy 2 obsadzane
/// miejscami z tabel fazy 1 (np. Grupa I: miejsca 1-2 z A i B, Grupa II: miejsca 3-4).
/// </summary>
public sealed class TwoGroupStagesRulesTests(IntegrationTestWebApplicationFactory factory) : IntegrationTestBase(factory)
{
    private static string T(TournamentScenario s) => $"/api/tournaments/{s.TournamentId}";

    private static async Task<HttpResponseMessage> AddSecondStageGroupAsync(TournamentScenario s, string name, int order = 1) =>
        await s.Organizer.PostAsJsonAsync($"{T(s)}/groups",
            new TournamentsController.AddTournamentGroupRequest(s.CategoryId, name, order, Stage: 2));

    private static async Task<Guid> SecondStageGroupAsync(TournamentScenario s, string name, int order = 1)
    {
        var response = await AddSecondStageGroupAsync(s, name, order);
        response.IsSuccessStatusCode.Should().BeTrue(await response.Content.ReadAsStringAsync());
        return (await response.ReadJsonAsync()).GetGuid("tournamentGroupId");
    }

    private static Task<HttpResponseMessage> PutSeatsAsync(TournamentScenario s, params (Guid Group, string Source, int Position)[] seats) =>
        s.Organizer.PutAsJsonAsync($"{T(s)}/second-stage/seats", new SecondStageController.SetSecondStageSeatsRequest(
            s.CategoryId, seats.Select(x => new SecondStageController.SecondStageSeatRequest(x.Group, s.Group(x.Source), x.Position)).ToList()));

    private static Task<HttpResponseMessage> PutRangesAsync(TournamentScenario s, params (Guid Group, int From, int To)[] ranges) =>
        s.Organizer.PutAsJsonAsync($"{T(s)}/second-stage/ranges", new SecondStageController.SetSecondStageRangesRequest(
            s.CategoryId, ranges.Select(x => new SecondStageController.SecondStageRangeRequest(x.Group, x.From, x.To)).ToList()));

    private static async Task<List<JsonElement>> SeatsAsync(TournamentScenario s) =>
        (await SecondStageAsync(s)).GetProperty("seats").EnumerateArray().ToList();

    private static async Task<JsonElement> SecondStageAsync(TournamentScenario s) =>
        await (await s.Organizer.GetAsync($"{T(s)}/second-stage")).ReadJsonAsync();

    /// <summary>
    /// Grupy A (T1-T3) i B (T4-T6), w fazie 2 Grupa I: miejsca 1-2 z A i B, Grupa II: miejsca 3 z A i B.
    /// </summary>
    private static async Task<(TournamentScenario S, Guid First, Guid Second)> TwoStagesAsync(
        IntegrationTestWebApplicationFactory factory, bool schedule = true, bool start = true)
    {
        var s = await TournamentScenario.GroupsAsync(factory, groups: 2, teamsPerGroup: 3, schedule: false, format: TournamentFormat.TwoGroupStages);
        var first = await SecondStageGroupAsync(s, "Grupa I", 1);
        var second = await SecondStageGroupAsync(s, "Grupa II", 2);
        var saved = await PutSeatsAsync(s,
            (first, "A", 1), (first, "B", 1), (first, "A", 2), (first, "B", 2),
            (second, "A", 3), (second, "B", 3));
        saved.IsSuccessStatusCode.Should().BeTrue(await saved.Content.ReadAsStringAsync());
        if (schedule)
            await s.GenerateScheduleAsync();
        if (schedule && start)
            await s.StartAsync();
        return (s, first, second);
    }

    private static Task<HttpResponseMessage> TryGenerateScheduleAsync(TournamentScenario s) =>
        s.Organizer.PostAsJsonAsync($"{T(s)}/schedule/generate",
            new MatchesController.GenerateScheduleRequest(s.StartsAt, s.StartsAt.AddHours(12), [s.PitchId]));

    private static async Task<List<JsonElement>> FixturesAsync(TournamentScenario s) =>
        (await SecondStageAsync(s)).GetProperty("fixtures").EnumerateArray().ToList();

    /// <summary>Mecz dwóch drużyn w danej grupie (te same drużyny mogą zagrać ze sobą w obu fazach).</summary>
    private static async Task<JsonElement> MatchInGroupAsync(TournamentScenario s, Guid groupId, string a, string b)
    {
        var (x, y) = (s.Team(a), s.Team(b));
        return (await s.ScheduleAsync()).Single(m => m.GetGuidOrNull("groupId") == groupId &&
            ((m.GetGuid("homeTournamentTeamId") == x && m.GetGuid("awayTournamentTeamId") == y) ||
             (m.GetGuid("homeTournamentTeamId") == y && m.GetGuid("awayTournamentTeamId") == x)));
    }

    /// <summary>Zwycięzca pierwszy: 1:0 dla <paramref name="winner"/>.</summary>
    private static async Task WinAsync(TournamentScenario s, Guid groupId, string winner, string loser)
    {
        var match = await MatchInGroupAsync(s, groupId, winner, loser);
        var winnerIsHome = match.GetGuid("homeTournamentTeamId") == s.Team(winner);
        await s.PlayAsync(match.GetGuid("id"), winnerIsHome ? 1 : 0, winnerIsHome ? 0 : 1);
    }

    /// <summary>Faza 1 tak, że kolejność to numer drużyny: A = T1, T2, T3; B = T4, T5, T6.</summary>
    private static async Task PlayFirstStageAsync(TournamentScenario s, string group = "AB")
    {
        if (group.Contains('A'))
            foreach (var (w, l) in new[] { ("T1", "T2"), ("T1", "T3"), ("T2", "T3") })
                await WinAsync(s, s.Group("A"), w, l);
        if (group.Contains('B'))
            foreach (var (w, l) in new[] { ("T4", "T5"), ("T4", "T6"), ("T5", "T6") })
                await WinAsync(s, s.Group("B"), w, l);
    }

    private static async Task<List<JsonElement>> TableAsync(TournamentScenario s, Guid groupId) =>
        (await (await s.Organizer.GetAsync($"{T(s)}/standings?groupId={groupId}")).ReadJsonAsync()).EnumerateArray().ToList();

    [Fact]
    public async Task FAZ_001_SecondStageGroups_ExistOnlyInTheTwoGroupStagesFormat()
    {
        var groups = await TournamentScenario.GroupsAsync(Factory, groups: 2, teamsPerGroup: 2, schedule: false);
        var rejected = await AddSecondStageGroupAsync(groups, "Grupa I");
        rejected.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await rejected.Content.ReadAsStringAsync()).Should().Contain("Second-stage groups need the two group stages format");

        var s = await TournamentScenario.GroupsAsync(Factory, groups: 2, teamsPerGroup: 2, schedule: false, format: TournamentFormat.TwoGroupStages);
        var groupId = await SecondStageGroupAsync(s, "Grupa I");

        var list = (await (await s.Organizer.GetAsync($"{T(s)}/groups")).ReadJsonAsync()).EnumerateArray().ToList();
        list.Single(g => g.GetGuid("id") == groupId).GetInt("stage").Should().Be(2);
        list.Single(g => g.GetGuid("id") == s.Group("A")).GetInt("stage").Should().Be(1);
    }

    [Fact]
    public async Task FAZ_002_TeamsCannotBeAssignedToASecondStageGroupByHand()
    {
        var s = await TournamentScenario.GroupsAsync(Factory, groups: 2, teamsPerGroup: 2, schedule: false, format: TournamentFormat.TwoGroupStages);
        var groupId = await SecondStageGroupAsync(s, "Grupa I");

        var response = await s.Organizer.PatchAsJsonAsync($"{T(s)}/tournament-teams/{s.Team("T1")}/group",
            new TournamentsController.AssignTeamToGroupRequest(groupId));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).Should().Contain("Teams reach second-stage groups from the first-stage tables");
    }

    [Fact]
    public async Task FAZ_003_FormatCannotChange_WhileSecondStageGroupsExist()
    {
        var s = await TournamentScenario.GroupsAsync(Factory, groups: 2, teamsPerGroup: 2, schedule: false, format: TournamentFormat.TwoGroupStages);
        await SecondStageGroupAsync(s, "Grupa I");

        var response = await s.Organizer.PatchAsJsonAsync($"{T(s)}/format",
            new TournamentsController.UpdateTournamentFormatRequest(TournamentFormat.Group));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).Should().Contain("Remove the second-stage groups before changing the format");
    }

    [Fact]
    public async Task FAZ_004_OrganizerSavesWhichPlacesGoToWhichSecondStageGroup_InOrder()
    {
        var s = await TournamentScenario.GroupsAsync(Factory, groups: 2, teamsPerGroup: 2, schedule: false, format: TournamentFormat.TwoGroupStages);
        var first = await SecondStageGroupAsync(s, "Grupa I", 1);
        var second = await SecondStageGroupAsync(s, "Grupa II", 2);

        (await PutSeatsAsync(s, (first, "A", 1), (first, "B", 1), (second, "A", 2), (second, "B", 2))).IsSuccessStatusCode.Should().BeTrue();
        // Drugi zapis zastępuje cały podział, także gdy te same miejsca trafiają gdzie indziej.
        var saved = await PutSeatsAsync(s, (first, "A", 1), (first, "B", 2), (second, "B", 1), (second, "A", 2));
        saved.IsSuccessStatusCode.Should().BeTrue(await saved.Content.ReadAsStringAsync());

        var seats = await SeatsAsync(s);
        seats.OrderBy(x => x.GetInt("order"))
            .Select(x => (x.GetGuid("groupId"), x.GetGuid("sourceGroupId"), x.GetInt("sourcePosition"), x.GetGuidOrNull("tournamentTeamId")))
            .Should().Equal(
                (first, s.Group("A"), 1, (Guid?)null), (first, s.Group("B"), 2, null),
                (second, s.Group("B"), 1, null), (second, s.Group("A"), 2, null));
    }

    [Fact]
    public async Task FAZ_005_APlaceCannotGoToTwoSecondStageGroups()
    {
        var s = await TournamentScenario.GroupsAsync(Factory, groups: 2, teamsPerGroup: 2, schedule: false, format: TournamentFormat.TwoGroupStages);
        var first = await SecondStageGroupAsync(s, "Grupa I", 1);
        var second = await SecondStageGroupAsync(s, "Grupa II", 2);

        var response = await PutSeatsAsync(s, (first, "A", 1), (second, "A", 1));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).Should().Contain("Each place can go to only one second-stage group");
        (await SeatsAsync(s)).Should().BeEmpty();
    }

    [Fact]
    public async Task FAZ_006_PlacesComeFromFirstStageGroups_AndGoToSecondStageGroups()
    {
        var s = await TournamentScenario.GroupsAsync(Factory, groups: 2, teamsPerGroup: 2, schedule: false, format: TournamentFormat.TwoGroupStages);
        var first = await SecondStageGroupAsync(s, "Grupa I", 1);

        var intoFirstStage = await PutSeatsAsync(s, (s.Group("B"), "A", 1));
        intoFirstStage.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await intoFirstStage.Content.ReadAsStringAsync()).Should().Contain("Each place must go to a second-stage group of this category");

        var fromSecondStage = await s.Organizer.PutAsJsonAsync($"{T(s)}/second-stage/seats", new SecondStageController.SetSecondStageSeatsRequest(
            s.CategoryId, [new SecondStageController.SecondStageSeatRequest(first, first, 1)]));
        fromSecondStage.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await fromSecondStage.Content.ReadAsStringAsync()).Should().Contain("Each place must come from a first-stage group of this category");
    }

    [Fact]
    public async Task FAZ_007_DeletingASecondStageGroup_RemovesItsPlaces()
    {
        var s = await TournamentScenario.GroupsAsync(Factory, groups: 2, teamsPerGroup: 2, schedule: false, format: TournamentFormat.TwoGroupStages);
        var first = await SecondStageGroupAsync(s, "Grupa I", 1);
        var second = await SecondStageGroupAsync(s, "Grupa II", 2);
        (await PutSeatsAsync(s, (first, "A", 1), (first, "B", 1), (second, "A", 2), (second, "B", 2))).IsSuccessStatusCode.Should().BeTrue();

        (await s.Organizer.DeleteAsync($"{T(s)}/groups/{second}")).IsSuccessStatusCode.Should().BeTrue();

        (await SeatsAsync(s)).Should().OnlyContain(x => x.GetGuid("groupId") == first).And.HaveCount(2);
    }

    [Fact]
    public async Task FAZ_008_TheSchedulePlansSecondStageMatchesBetweenPlaces_AfterTheirSourceGroups()
    {
        var (s, first, second) = await TwoStagesAsync(Factory, start: false);

        var matches = await s.ScheduleAsync();
        matches.Should().HaveCount(6, "faza 1: dwie grupy po 3 drużyny");
        var lastFirstStage = matches.Max(m => DateTimeOffset.Parse(m.GetString("scheduledAt")));

        var fixtures = await FixturesAsync(s);
        fixtures.Count(f => f.GetGuid("groupId") == first).Should().Be(6, "Grupa I: 4 miejsca, każdy z każdym");
        fixtures.Count(f => f.GetGuid("groupId") == second).Should().Be(1);
        fixtures.Should().OnlyContain(f => f.GetGuidOrNull("matchId") == null && f.GetGuidOrNull("plannedPitchId") == s.PitchId);
        fixtures.Should().OnlyContain(f => DateTimeOffset.Parse(f.GetProperty("plannedAt").GetString()!) > lastFirstStage);
        fixtures.Select(f => DateTimeOffset.Parse(f.GetProperty("plannedAt").GetString()!)).Should().OnlyHaveUniqueItems();
        (await SeatsAsync(s)).Should().OnlyContain(x => x.GetGuidOrNull("tournamentTeamId") == null);
    }

    [Fact]
    public async Task FAZ_009_TheScheduleNeedsSecondStageGroupsWithExistingPlaces()
    {
        var s = await TournamentScenario.GroupsAsync(Factory, groups: 2, teamsPerGroup: 3, schedule: false, format: TournamentFormat.TwoGroupStages);
        var none = await TryGenerateScheduleAsync(s);
        none.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await none.Content.ReadAsStringAsync()).Should().Contain("Every category needs second-stage groups with places");

        var first = await SecondStageGroupAsync(s, "Grupa I", 1);
        (await PutSeatsAsync(s, (first, "A", 1))).IsSuccessStatusCode.Should().BeTrue();
        var alone = await TryGenerateScheduleAsync(s);
        alone.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await alone.Content.ReadAsStringAsync()).Should().Contain("Each second-stage group needs at least 2 places");

        (await PutSeatsAsync(s, (first, "A", 1), (first, "B", 4))).IsSuccessStatusCode.Should().BeTrue();
        var missing = await TryGenerateScheduleAsync(s);
        missing.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await missing.Content.ReadAsStringAsync()).Should().Contain("A second-stage place points to a position its group does not have");
        (await s.ScheduleAsync()).Should().BeEmpty();
    }

    [Fact]
    public async Task FAZ_010_TheSplitIsLockedWhileTheScheduleExists_AndClearingTheScheduleRemovesPlannedMatches()
    {
        var (s, first, second) = await TwoStagesAsync(Factory, start: false);

        var locked = await PutSeatsAsync(s, (first, "A", 1), (first, "B", 1));
        locked.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await locked.Content.ReadAsStringAsync()).Should().Contain("Clear the schedule before changing the second stage");

        (await s.Organizer.DeleteAsync($"{T(s)}/schedule")).IsSuccessStatusCode.Should().BeTrue();

        (await FixturesAsync(s)).Should().BeEmpty();
        (await SeatsAsync(s)).Should().HaveCount(6, "podział zostaje, znikają tylko mecze");
        (await PutSeatsAsync(s, (first, "A", 1), (first, "B", 1), (second, "A", 2), (second, "B", 2))).IsSuccessStatusCode.Should().BeTrue();
    }

    [Fact]
    public async Task FAZ_011_FinishedFirstStageGroups_FillTheirPlaces_AndSecondStageMatchesAppearAtThePlannedTime()
    {
        var (s, first, second) = await TwoStagesAsync(Factory);
        var planned = await FixturesAsync(s);

        await PlayFirstStageAsync(s, "A");
        await FlushOutboxAsync();
        var afterA = await SeatsAsync(s);
        afterA.Where(x => x.GetGuid("sourceGroupId") == s.Group("A"))
            .Select(x => (x.GetInt("sourcePosition"), x.GetGuidOrNull("tournamentTeamId")))
            .Should().BeEquivalentTo([(1, (Guid?)s.Team("T1")), (2, s.Team("T2")), (3, s.Team("T3"))]);
        afterA.Where(x => x.GetGuid("sourceGroupId") == s.Group("B")).Should().OnlyContain(x => x.GetGuidOrNull("tournamentTeamId") == null);
        // Obie drużyny meczu 1A-2A są już znane, reszta czeka na grupę B.
        (await s.ScheduleAsync()).Where(m => m.GetGuidOrNull("groupId") == first || m.GetGuidOrNull("groupId") == second)
            .Should().ContainSingle().Which.GetGuid("id").Should().Be((await MatchInGroupAsync(s, first, "T1", "T2")).GetGuid("id"));

        await PlayFirstStageAsync(s, "B");
        await FlushOutboxAsync();

        var secondStage = (await s.ScheduleAsync()).Where(m => m.GetGuidOrNull("groupId") == first || m.GetGuidOrNull("groupId") == second).ToList();
        secondStage.Should().HaveCount(7);
        secondStage.Should().OnlyContain(m => m.GetString("stageType") == "Group" && m.GetString("status") == "Scheduled");
        var fixtures = await FixturesAsync(s);
        fixtures.Should().OnlyContain(f => f.GetGuidOrNull("matchId") != null);
        foreach (var fixture in fixtures)
        {
            var match = secondStage.Single(m => m.GetGuid("id") == fixture.GetGuid("matchId"));
            var plannedAt = planned.Single(f => f.GetGuid("id") == fixture.GetGuid("id")).GetProperty("plannedAt").GetString();
            DateTimeOffset.Parse(match.GetString("scheduledAt")).Should().Be(DateTimeOffset.Parse(plannedAt!));
        }
        await MatchInGroupAsync(s, second, "T3", "T6");
        await MatchInGroupAsync(s, first, "T1", "T5");
    }

    [Fact]
    public async Task FAZ_012_TheSecondStageTableStartsFromZero()
    {
        var (s, first, _) = await TwoStagesAsync(Factory);
        await PlayFirstStageAsync(s);
        await FlushOutboxAsync();

        var before = await TableAsync(s, first);
        before.Select(r => r.GetGuid("tournamentTeamId")).Should().BeEquivalentTo(new[] { s.Team("T1"), s.Team("T2"), s.Team("T4"), s.Team("T5") });
        before.Should().OnlyContain(r => r.GetInt("points") == 0 && r.GetInt("matchesPlayed") == 0);

        // T1 i T2 grały już ze sobą w grupie A; w fazie 2 liczy się tylko nowy mecz.
        await WinAsync(s, first, "T2", "T1");
        await FlushOutboxAsync();

        var table = await TableAsync(s, first);
        table.Single(r => r.GetGuid("tournamentTeamId") == s.Team("T2")).GetInt("matchesPlayed").Should().Be(1);
        table.Single(r => r.GetGuid("tournamentTeamId") == s.Team("T2")).GetInt("position").Should().Be(1);
        table.Single(r => r.GetGuid("tournamentTeamId") == s.Team("T1")).GetInt("points").Should().Be(0);
        table.Single(r => r.GetGuid("tournamentTeamId") == s.Team("T4")).GetInt("matchesPlayed").Should().Be(0);
    }

    [Fact]
    public async Task FAZ_013_CorrectionAfterTheFirstStage_MovesTeamsInUnstartedSecondStageMatches_AndNotifiesAboutStartedOnes()
    {
        var (s, first, second) = await TwoStagesAsync(Factory);
        await PlayFirstStageAsync(s);
        await FlushOutboxAsync();
        var started = (await MatchInGroupAsync(s, first, "T1", "T4")).GetGuid("id");
        (await s.Organizer.PostAsync($"{T(s)}/matches/{started}/start", null)).IsSuccessStatusCode.Should().BeTrue();
        var waiting = (await MatchInGroupAsync(s, second, "T3", "T6")).GetGuid("id");

        // T3 jednak wygrywa z T1: w grupie A 1. T2, 2. T3, 3. T1 (różnica bramek przy równych punktach).
        var groupMatch = await MatchInGroupAsync(s, s.Group("A"), "T1", "T3");
        var t3Home = groupMatch.GetGuid("homeTournamentTeamId") == s.Team("T3");
        var corrected = await s.Organizer.PatchAsJsonAsync($"{T(s)}/matches/{groupMatch.GetGuid("id")}/correct-score",
            new { homeScore = t3Home ? 5 : 0, awayScore = t3Home ? 0 : 5, winnerTournamentTeamId = s.Team("T3") });
        corrected.IsSuccessStatusCode.Should().BeTrue(await corrected.Content.ReadAsStringAsync());
        await FlushOutboxAsync();

        var seats = await SeatsAsync(s);
        seats.Single(x => x.GetGuid("sourceGroupId") == s.Group("A") && x.GetInt("sourcePosition") == 3)
            .GetGuidOrNull("tournamentTeamId").Should().Be(s.Team("T1"));
        (await MatchInGroupAsync(s, second, "T1", "T6")).GetGuid("id").Should().Be(waiting, "nierozpoczęty mecz dostaje nową drużynę");
        (await MatchInGroupAsync(s, first, "T1", "T4")).GetGuid("id").Should().Be(started, "rozpoczęty mecz zostaje");
        (await s.NotificationsAsync(s.Organizer)).Should().Contain(n => n.GetString("type") == "BracketChangeBlocked");
    }

    [Fact]
    public async Task FAZ_014_TeamHistory_ShowsThePlaceInTheSecondStageGroup()
    {
        var (s, first, _) = await TwoStagesAsync(Factory);
        await PlayFirstStageAsync(s);
        await FlushOutboxAsync();
        await WinAsync(s, first, "T2", "T1");
        await FlushOutboxAsync();

        var history = await (await s.Organizer.GetAsync($"/api/teams/{s.ClubTeam("T2")}/history")).ReadJsonAsync();

        var entry = history.GetProperty("tournaments").EnumerateArray().Single();
        entry.GetString("resultKind").Should().Be("Group");
        entry.GetProperty("groupPosition").GetInt32().Should().Be(1);
        entry.GetString("groupName").Should().Be("Grupa I");
    }

    [Fact]
    public async Task FAZ_015_PublicPage_ShowsTheSecondStage_OnlyWithScheduleOrStandings()
    {
        var (s, first, _) = await TwoStagesAsync(Factory, start: false);
        var slug = (await s.TournamentAsync()).GetString("slug");
        var anonymous = CreateClient();
        Task<HttpResponseMessage> Publish(bool schedule, bool standings) => s.Organizer.PutAsJsonAsync($"{T(s)}/public-settings",
            new { isPublic = true, showTeams = true, showPlayers = true, showSchedule = schedule, showStandings = standings, showBracket = true, showLiveMatches = true, showStats = true, abbreviatePlayerNames = true, showInDirectory = true });

        (await Publish(schedule: true, standings: false)).IsSuccessStatusCode.Should().BeTrue();
        var page = await (await anonymous.GetAsync($"/public/tournaments/{slug}")).ReadJsonAsync();
        page.GetProperty("groups").EnumerateArray().Single(g => g.GetGuid("id") == first).GetInt("stage").Should().Be(2);
        var visible = await (await anonymous.GetAsync($"/public/tournaments/{slug}/second-stage")).ReadJsonAsync();
        visible.GetProperty("seats").GetArrayLength().Should().Be(6);
        visible.GetProperty("fixtures").GetArrayLength().Should().Be(7);

        (await Publish(schedule: false, standings: false)).IsSuccessStatusCode.Should().BeTrue();
        var hidden = await (await anonymous.GetAsync($"/public/tournaments/{slug}/second-stage")).ReadJsonAsync();
        hidden.GetProperty("seats").GetArrayLength().Should().Be(0);
        hidden.GetProperty("fixtures").GetArrayLength().Should().Be(0);
    }

    [Fact]
    public async Task FAZ_026_RangesOfPlaces_TurnIntoPlacesFromTheRealTablesWhenTheScheduleIsGenerated()
    {
        var s = await TournamentScenario.GroupsAsync(Factory, groups: 2, teamsPerGroup: 3, schedule: false, format: TournamentFormat.TwoGroupStages);
        var first = await SecondStageGroupAsync(s, "Grupa I", 1);
        var second = await SecondStageGroupAsync(s, "Grupa II", 2);
        // Ustawione jak w kreatorze, bez wiedzy o liczbie drużyn: grupy mają po 3, a nie po 4.
        (await PutRangesAsync(s, (first, 1, 2), (second, 3, 4))).IsSuccessStatusCode.Should().BeTrue();
        (await SecondStageAsync(s)).GetProperty("ranges").GetArrayLength().Should().Be(2);
        (await SeatsAsync(s)).Should().BeEmpty();

        await s.GenerateScheduleAsync();

        var seats = await SeatsAsync(s);
        seats.Where(x => x.GetGuid("groupId") == first).Select(x => (x.GetInt("sourcePosition"), x.GetGuid("sourceGroupId")))
            .Should().BeEquivalentTo([(1, s.Group("A")), (1, s.Group("B")), (2, s.Group("A")), (2, s.Group("B"))]);
        seats.Where(x => x.GetGuid("groupId") == second).Select(x => x.GetInt("sourcePosition")).Should().Equal(3, 3);
        (await FixturesAsync(s)).Should().HaveCount(6 + 1);
    }

    [Fact]
    public async Task FAZ_027_RangesCannotOverlap()
    {
        var s = await TournamentScenario.GroupsAsync(Factory, groups: 2, teamsPerGroup: 2, schedule: false, format: TournamentFormat.TwoGroupStages);
        var first = await SecondStageGroupAsync(s, "Grupa I", 1);
        var second = await SecondStageGroupAsync(s, "Grupa II", 2);

        var response = await PutRangesAsync(s, (first, 1, 2), (second, 2, 3));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).Should().Contain("Each place can go to only one second-stage group");
    }

    [Fact]
    public async Task FAZ_028_WithoutASplit_TheScheduleUsesTheReadyOneSplitEvenly()
    {
        var s = await TournamentScenario.GroupsAsync(Factory, groups: 2, teamsPerGroup: 3, schedule: false, format: TournamentFormat.TwoGroupStages);
        var first = await SecondStageGroupAsync(s, "Grupa I", 1);
        var second = await SecondStageGroupAsync(s, "Grupa II", 2);

        await s.GenerateScheduleAsync();

        var ranges = (await SecondStageAsync(s)).GetProperty("ranges").EnumerateArray().ToList();
        ranges.Select(r => (r.GetGuid("groupId"), r.GetInt("fromPosition"), r.GetInt("toPosition")))
            .Should().BeEquivalentTo([(first, 1, 2), (second, 3, 3)]);
        (await SeatsAsync(s)).Should().HaveCount(6);
    }

    [Fact]
    public async Task FAZ_029_RangesAndSinglePlaces_ReplaceEachOther()
    {
        var s = await TournamentScenario.GroupsAsync(Factory, groups: 2, teamsPerGroup: 2, schedule: false, format: TournamentFormat.TwoGroupStages);
        var first = await SecondStageGroupAsync(s, "Grupa I", 1);

        (await PutSeatsAsync(s, (first, "A", 1), (first, "B", 1))).IsSuccessStatusCode.Should().BeTrue();
        (await PutRangesAsync(s, (first, 1, 2))).IsSuccessStatusCode.Should().BeTrue();
        (await SeatsAsync(s)).Should().BeEmpty();

        (await PutSeatsAsync(s, (first, "A", 1), (first, "B", 2))).IsSuccessStatusCode.Should().BeTrue();
        var stage = await SecondStageAsync(s);
        stage.GetProperty("ranges").GetArrayLength().Should().Be(0);
        stage.GetProperty("seats").GetArrayLength().Should().Be(2);
    }
}

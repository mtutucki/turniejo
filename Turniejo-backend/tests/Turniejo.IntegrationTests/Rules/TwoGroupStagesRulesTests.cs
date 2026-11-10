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

    private static async Task<List<JsonElement>> SeatsAsync(TournamentScenario s) =>
        (await (await s.Organizer.GetAsync($"{T(s)}/second-stage/seats")).ReadJsonAsync()).EnumerateArray().ToList();

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
}

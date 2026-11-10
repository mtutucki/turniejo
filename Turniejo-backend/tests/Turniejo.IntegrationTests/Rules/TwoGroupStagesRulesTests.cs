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
}

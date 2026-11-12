using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Turniejo.Api.Controllers;
using Turniejo.IntegrationTests.Infrastructure;

namespace Turniejo.IntegrationTests.Rules;

/// <summary>
/// Boiska przypisane do grup: turniej w dwóch halach, w jednej boiska A i B dla grupy A, w drugiej
/// C i D dla grupy B. Generator kładzie mecze grupy tylko na jej boiskach; boisko bez grup przyjmuje każdą.
/// </summary>
public sealed class PitchGroupsRulesTests(IntegrationTestWebApplicationFactory factory) : IntegrationTestBase(factory)
{
    private static string T(TournamentScenario s) => $"/api/tournaments/{s.TournamentId}";

    private static async Task<Guid> AddPitchAsync(TournamentScenario s, string name, params Guid[] groupIds)
    {
        var response = await s.Organizer.PostAsJsonAsync($"{T(s)}/pitches",
            new MatchesController.AddPitchRequest(name, null, groupIds));
        response.IsSuccessStatusCode.Should().BeTrue(await response.Content.ReadAsStringAsync());
        return (await response.ReadJsonAsync()).GetGuid("pitchId");
    }

    private static Task<HttpResponseMessage> GenerateAsync(TournamentScenario s, params Guid[] pitchIds) =>
        s.Organizer.PostAsJsonAsync($"{T(s)}/schedule/generate",
            new MatchesController.GenerateScheduleRequest(s.StartsAt, s.StartsAt.AddHours(12), pitchIds));

    [Fact]
    public async Task GroupMatches_ArePlayedOnlyOnPitchesAssignedToTheirGroup()
    {
        var s = await TournamentScenario.GroupsAsync(factory, groups: 2, teamsPerGroup: 4, schedule: false);
        var (groupA, groupB) = (s.Group("A"), s.Group("B"));
        var hallOne = new[] { await AddPitchAsync(s, "A", groupA), await AddPitchAsync(s, "B", groupA) };
        var hallTwo = new[] { await AddPitchAsync(s, "C", groupB), await AddPitchAsync(s, "D", groupB) };

        var response = await GenerateAsync(s, [.. hallOne, .. hallTwo]);

        response.IsSuccessStatusCode.Should().BeTrue(await response.Content.ReadAsStringAsync());
        var matches = await s.ScheduleAsync();
        matches.Should().HaveCount(12);
        matches.Where(m => m.GetGuid("groupId") == groupA).Select(m => m.GetGuid("pitchId"))
            .Should().HaveCount(6).And.OnlyContain(p => hallOne.Contains(p));
        matches.Where(m => m.GetGuid("groupId") == groupB).Select(m => m.GetGuid("pitchId"))
            .Should().HaveCount(6).And.OnlyContain(p => hallTwo.Contains(p));
    }

    [Fact]
    public async Task PitchWithoutGroups_TakesMatchesOfEveryGroup()
    {
        var s = await TournamentScenario.GroupsAsync(factory, groups: 2, teamsPerGroup: 3, schedule: false);
        var onlyA = await AddPitchAsync(s, "Tylko A", s.Group("A"));

        // Boisko ze scenariusza nie ma grup: grupa B gra tylko na nim, grupa A na obu.
        var response = await GenerateAsync(s, s.PitchId, onlyA);

        response.IsSuccessStatusCode.Should().BeTrue(await response.Content.ReadAsStringAsync());
        var matches = await s.ScheduleAsync();
        matches.Where(m => m.GetGuid("groupId") == s.Group("B"))
            .Should().OnlyContain(m => m.GetGuid("pitchId") == s.PitchId);
    }

    [Fact]
    public async Task GroupWithoutAnySelectedPitch_BlocksTheSchedule()
    {
        var s = await TournamentScenario.GroupsAsync(factory, groups: 2, teamsPerGroup: 3, schedule: false);
        var onlyA = await AddPitchAsync(s, "Tylko A", s.Group("A"));

        var response = await GenerateAsync(s, onlyA);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).Should().Contain("Some groups have no selected pitch assigned to them");
        (await s.ScheduleAsync()).Should().BeEmpty();
    }

    [Fact]
    public async Task PitchGroups_AreReturnedWithThePitch_AndDeletedGroupDisappearsFromThem()
    {
        var s = await TournamentScenario.GroupsAsync(factory, groups: 1, teamsPerGroup: 2, schedule: false);
        var groupResponse = await s.Organizer.PostAsJsonAsync($"{T(s)}/groups",
            new TournamentsController.AddTournamentGroupRequest(s.CategoryId, "Pusta", 2));
        var emptyGroup = (await groupResponse.ReadJsonAsync()).GetGuid("tournamentGroupId");
        var pitchId = await AddPitchAsync(s, "Hala 2", s.Group("A"), emptyGroup);

        (await s.Organizer.DeleteAsync($"{T(s)}/groups/{emptyGroup}")).IsSuccessStatusCode.Should().BeTrue();

        var pitch = (await (await s.Organizer.GetAsync($"{T(s)}/pitches")).ReadJsonAsync())
            .EnumerateArray().Single(p => p.GetGuid("id") == pitchId);
        pitch.GetProperty("groupIds").EnumerateArray().Select(g => g.GetGuid()).Should().Equal(s.Group("A"));
    }

    [Fact]
    public async Task GroupFromAnotherTournament_CannotBeAssignedToAPitch()
    {
        var mine = await TournamentScenario.GroupsAsync(factory, groups: 1, teamsPerGroup: 2, schedule: false);
        var other = await TournamentScenario.GroupsAsync(factory, groups: 1, teamsPerGroup: 2, schedule: false);

        var response = await mine.Organizer.PutAsJsonAsync($"{T(mine)}/pitches/{mine.PitchId}",
            new MatchesController.UpdatePitchRequest("Boisko 1", null, true, [other.Group("A")]));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}

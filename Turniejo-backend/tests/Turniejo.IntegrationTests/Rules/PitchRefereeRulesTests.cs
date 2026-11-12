using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Turniejo.Api.Controllers;
using Turniejo.IntegrationTests.Infrastructure;

namespace Turniejo.IntegrationTests.Rules;

/// <summary>
/// Sędzia przypisany do boiska prowadzi jego mecze: nierozpoczęte bez sędziego albo z poprzednim
/// sędzią boiska. Ręcznie wybrany inny sędzia zostaje. Jeden sędzia prowadzi jedno boisko.
/// </summary>
public sealed class PitchRefereeRulesTests(IntegrationTestWebApplicationFactory factory) : IntegrationTestBase(factory)
{
    private static string T(TournamentScenario s) => $"/api/tournaments/{s.TournamentId}";

    private static Task<HttpResponseMessage> SetPitchRefereeAsync(TournamentScenario s, Guid pitchId, string name, Guid? refereeId) =>
        s.Organizer.PutAsJsonAsync($"{T(s)}/pitches/{pitchId}",
            new MatchesController.UpdatePitchRequest(name, null, true, null, refereeId));

    private static async Task<Guid> AddPitchAsync(TournamentScenario s, string name, Guid? refereeId = null)
    {
        var response = await s.Organizer.PostAsJsonAsync($"{T(s)}/pitches",
            new MatchesController.AddPitchRequest(name, null, null, refereeId));
        response.IsSuccessStatusCode.Should().BeTrue(await response.Content.ReadAsStringAsync());
        return (await response.ReadJsonAsync()).GetGuid("pitchId");
    }

    private static Guid? RefereeOf(System.Text.Json.JsonElement match) => match.GetGuidOrNull("refereeId");

    [Fact]
    public async Task GeneratedSchedule_GivesEveryMatchOnThePitchItsReferee()
    {
        var s = await TournamentScenario.GroupsAsync(Factory, teamsPerGroup: 4, schedule: false);
        var (_, referee) = await s.JoinAsync("Referee");
        (await SetPitchRefereeAsync(s, s.PitchId, "Boisko 1", referee)).IsSuccessStatusCode.Should().BeTrue();

        await s.GenerateScheduleAsync();

        (await s.ScheduleAsync()).Should().HaveCount(6).And.OnlyContain(m => RefereeOf(m) == referee);
    }

    [Fact]
    public async Task SettingThePitchReferee_TakesUnassignedMatches_AndKeepsManualChoicesAndStartedMatches()
    {
        var s = await TournamentScenario.GroupsAsync(Factory, teamsPerGroup: 4);
        var (_, manual) = await s.JoinAsync("Referee");
        var (refereeClient, pitchReferee) = await s.JoinAsync("Referee");
        var manualMatch = (await s.MatchBetweenAsync("T1", "T2")).GetGuid("id");
        await s.AssignRefereeAsync(manualMatch, manual);
        var started = (await s.MatchBetweenAsync("T3", "T4")).GetGuid("id");
        (await s.Organizer.PostAsync($"{T(s)}/matches/{started}/start", null)).IsSuccessStatusCode.Should().BeTrue();

        (await SetPitchRefereeAsync(s, s.PitchId, "Boisko 1", pitchReferee)).IsSuccessStatusCode.Should().BeTrue();

        var matches = await s.ScheduleAsync();
        RefereeOf(matches.Single(m => m.GetGuid("id") == manualMatch)).Should().Be(manual);
        RefereeOf(matches.Single(m => m.GetGuid("id") == started)).Should().BeNull();
        matches.Where(m => m.GetGuid("id") != manualMatch && m.GetGuid("id") != started)
            .Should().HaveCount(4).And.OnlyContain(m => RefereeOf(m) == pitchReferee);

        // Przypisany sędzia ma rolę w meczu: może go poprowadzić.
        var hisMatch = matches.First(m => RefereeOf(m) == pitchReferee).GetGuid("id");
        (await refereeClient.PostAsync($"{T(s)}/matches/{hisMatch}/start", null)).IsSuccessStatusCode.Should().BeTrue();
    }

    [Fact]
    public async Task ChangingThePitchReferee_MovesHisMatches_AndClearingReturnsThemToThePool()
    {
        var s = await TournamentScenario.GroupsAsync(Factory, teamsPerGroup: 3);
        var (_, first) = await s.JoinAsync("Referee");
        var (_, second) = await s.JoinAsync("Referee");
        (await SetPitchRefereeAsync(s, s.PitchId, "Boisko 1", first)).IsSuccessStatusCode.Should().BeTrue();

        (await SetPitchRefereeAsync(s, s.PitchId, "Boisko 1", second)).IsSuccessStatusCode.Should().BeTrue();
        (await s.ScheduleAsync()).Should().OnlyContain(m => RefereeOf(m) == second);

        (await SetPitchRefereeAsync(s, s.PitchId, "Boisko 1", null)).IsSuccessStatusCode.Should().BeTrue();
        (await s.ScheduleAsync()).Should().OnlyContain(m => RefereeOf(m) == null);
    }

    [Fact]
    public async Task MatchMovedToAnotherPitch_TakesThatPitchsReferee()
    {
        var s = await TournamentScenario.GroupsAsync(Factory, teamsPerGroup: 3);
        var (_, hallOne) = await s.JoinAsync("Referee");
        var (_, hallTwo) = await s.JoinAsync("Referee");
        (await SetPitchRefereeAsync(s, s.PitchId, "Boisko 1", hallOne)).IsSuccessStatusCode.Should().BeTrue();
        var otherPitch = await AddPitchAsync(s, "Boisko 2", hallTwo);
        var match = (await s.MatchBetweenAsync("T1", "T2")).GetGuid("id");

        (await s.Organizer.PatchAsJsonAsync($"{T(s)}/matches/{match}/pitch",
            new MatchesController.AssignMatchPitchRequest(otherPitch))).IsSuccessStatusCode.Should().BeTrue();

        RefereeOf((await s.ScheduleAsync()).Single(m => m.GetGuid("id") == match)).Should().Be(hallTwo);
    }

    [Fact]
    public async Task CupMatchCreatedLater_GetsThePitchReferee()
    {
        var s = await TournamentScenario.KnockoutAsync(Factory, teams: 4, schedule: false);
        var (_, referee) = await s.JoinAsync("Referee");
        (await SetPitchRefereeAsync(s, s.PitchId, "Boisko 1", referee)).IsSuccessStatusCode.Should().BeTrue();
        await s.GenerateScheduleAsync();
        await TournamentFlow.ChangeStatusAsync(s.Organizer, s.TournamentId, Turniejo.Tournaments.Domain.Enums.TournamentStatus.InProgress);

        await s.PlayAsync((await s.MatchBetweenAsync("T1", "T2")).GetGuid("id"), 1, 0);
        await s.PlayAsync((await s.MatchBetweenAsync("T3", "T4")).GetGuid("id"), 1, 0);
        await FlushOutboxAsync();

        var final = await s.MatchBetweenAsync("T1", "T3");
        RefereeOf(final).Should().Be(referee);
    }

    [Fact]
    public async Task OneRefereeLeadsOnePitch_AndOnlyTournamentRefereesCanBeChosen()
    {
        var s = await TournamentScenario.GroupsAsync(Factory, teamsPerGroup: 2, schedule: false);
        var (_, referee) = await s.JoinAsync("Referee");
        var (_, coach) = await s.JoinAsync("Coach");
        (await SetPitchRefereeAsync(s, s.PitchId, "Boisko 1", referee)).IsSuccessStatusCode.Should().BeTrue();

        var second = await s.Organizer.PostAsJsonAsync($"{T(s)}/pitches",
            new MatchesController.AddPitchRequest("Boisko 2", null, null, referee));
        second.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await second.Content.ReadAsStringAsync()).Should().Contain("The referee is already assigned to another pitch");

        (await SetPitchRefereeAsync(s, s.PitchId, "Boisko 1", coach)).StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var pitch = (await (await s.Organizer.GetAsync($"{T(s)}/pitches")).ReadJsonAsync())
            .EnumerateArray().Single(p => p.GetGuid("id") == s.PitchId);
        pitch.GetGuidOrNull("refereeId").Should().Be(referee);
    }
}

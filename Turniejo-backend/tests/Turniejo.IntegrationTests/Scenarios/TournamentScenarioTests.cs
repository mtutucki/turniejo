using FluentAssertions;
using Turniejo.IntegrationTests.Infrastructure;

namespace Turniejo.IntegrationTests.Scenarios;

// Pilnuje samego pomocnika: jeśli przestanie budować poprawny turniej, padną od razu wszystkie
// testy, które z niego korzystają - ten test mówi wprost, gdzie szukać przyczyny.
public sealed class TournamentScenarioTests(IntegrationTestWebApplicationFactory factory) : IntegrationTestBase(factory)
{
    [Fact]
    public async Task GroupScenario_SchedulesRoundRobin_AndPlayedMatchUpdatesTheTable()
    {
        var s = await TournamentScenario.GroupsAsync(Factory, groups: 1, teamsPerGroup: 4);

        var schedule = await s.ScheduleAsync();
        schedule.Should().HaveCount(6);

        var match = await s.MatchBetweenAsync("T1", "T2");
        await s.PlayAsync(match.GetGuid("id"), 2, 0);

        var table = (await (await s.Organizer.GetAsync($"/api/tournaments/{s.TournamentId}/standings?groupId={s.Group("A")}")).ReadJsonAsync())
            .EnumerateArray().ToList();
        table.Should().Contain(row => row.GetInt("points") == 3);
    }

    [Fact]
    public async Task KnockoutScenario_CreatesFirstRoundMatches_AndJoinedRefereeSeesTheTournament()
    {
        var s = await TournamentScenario.KnockoutAsync(Factory, teams: 4, thirdPlace: true);

        (await s.ScheduleAsync()).Should().HaveCount(2);

        var (referee, _) = await s.JoinAsync("Referee");
        var roles = (await (await referee.GetAsync("/api/me/roles")).ReadJsonAsync()).EnumerateArray().ToList();
        roles.Should().Contain(r => r.GetString("role") == "Referee" && r.GetGuid("scopeId") == s.TournamentId);
    }
}

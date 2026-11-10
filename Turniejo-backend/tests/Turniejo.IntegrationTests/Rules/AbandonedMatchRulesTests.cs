using System.Text.Json;
using FluentAssertions;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Turniejo.IntegrationTests.Infrastructure;
using Turniejo.Matches.Application.Matches.FinishAbandonedMatches;

namespace Turniejo.IntegrationTests.Rules;

/// <summary>
/// Mecz, którego nikt nie zakończył (sędzia zamknął telefon), trwał w nieskończoność, a zegar
/// pokazywał tysiące minut. Po 3× czasie meczu (min. 2 h) od startu backend kończy go sam.
/// </summary>
public sealed class AbandonedMatchRulesTests(IntegrationTestWebApplicationFactory factory) : IntegrationTestBase(factory)
{
    private async Task<int> RunAsync(TimeSpan after)
    {
        using var scope = Factory.Services.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();
        var result = await sender.Send(new FinishAbandonedMatchesCommand(DateTimeOffset.UtcNow + after));
        result.IsSuccess.Should().BeTrue();
        await FlushOutboxAsync();
        return result.Value.FinishedCount;
    }

    private static async Task<JsonElement> MatchAsync(TournamentScenario s, Guid id) =>
        (await s.ScheduleAsync()).Single(m => m.GetGuid("id") == id);

    private static async Task<Guid> StartGroupMatchAsync(TournamentScenario s, int homeGoals)
    {
        var match = await s.MatchBetweenAsync("T1", "T2");
        var id = match.GetGuid("id");
        (await s.Organizer.PostAsync($"/api/tournaments/{s.TournamentId}/matches/{id}/start", null)).IsSuccessStatusCode.Should().BeTrue();
        for (var i = 0; i < homeGoals; i++)
            await TournamentFlow.AddGoalAsync(s.Organizer, s.TournamentId, id, match.GetGuid("homeTournamentTeamId"), 10 + i);
        return id;
    }

    [Fact]
    public async Task MNZ_040_MatchLeftLiveTooLong_IsFinishedWithItsCurrentScore()
    {
        var s = await TournamentScenario.GroupsAsync(Factory, teamsPerGroup: 4);
        var id = await StartGroupMatchAsync(s, homeGoals: 2);

        // 30 min meczu → próg 2 h (minimum).
        (await RunAsync(TimeSpan.FromHours(2.5))).Should().Be(1);

        var match = await MatchAsync(s, id);
        match.GetString("status").Should().Be("Finished");
        match.GetInt("homeScore").Should().Be(2);
        match.GetInt("awayScore").Should().Be(0);

        var table = (await (await s.Organizer.GetAsync($"/api/tournaments/{s.TournamentId}/standings?groupId={s.Group("A")}")).ReadJsonAsync())
            .EnumerateArray().ToList();
        table.Sum(r => r.GetInt("matchesPlayed")).Should().Be(2);
    }

    [Fact]
    public async Task MNZ_041_MatchWithinThreeTimesItsDuration_KeepsGoing()
    {
        var s = await TournamentScenario.GroupsAsync(Factory, teamsPerGroup: 4, start: false);
        await s.UpdateSettingsAsync(matchMinutes: 60);
        await s.StartAsync();
        var id = await StartGroupMatchAsync(s, homeGoals: 0);

        // 60 min meczu → próg 3 h.
        (await RunAsync(TimeSpan.FromHours(2.5))).Should().Be(0);
        (await MatchAsync(s, id)).GetString("status").Should().Be("Live");

        (await RunAsync(TimeSpan.FromHours(3.5))).Should().Be(1);
        (await MatchAsync(s, id)).GetString("status").Should().Be("Finished");
    }

    [Fact]
    public async Task MNZ_042_KnockoutDraw_IsLeftForTheOrganizer()
    {
        var s = await TournamentScenario.KnockoutAsync(Factory);
        var id = (await s.ScheduleAsync()).First().GetGuid("id");
        (await s.Organizer.PostAsync($"/api/tournaments/{s.TournamentId}/matches/{id}/start", null)).IsSuccessStatusCode.Should().BeTrue();

        (await RunAsync(TimeSpan.FromHours(5))).Should().Be(0);
        (await MatchAsync(s, id)).GetString("status").Should().Be("Live");
    }
}

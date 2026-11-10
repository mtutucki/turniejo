using System.Text.Json;
using FluentAssertions;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Turniejo.IntegrationTests.Infrastructure;
using Turniejo.Matches.Application.Matches.FinishAbandonedMatches;

namespace Turniejo.IntegrationTests.Rules;

/// <summary>
/// Mecz, którego nikt nie zakończył (sędzia zamknął telefon), trwał w nieskończoność, a zegar
/// pokazywał tysiące minut. Gdy od startu minęło 3× czasu meczu i od 4 h nikt nic przy nim nie
/// zrobił, backend kończy go sam. Aktywność przy meczu sprawdzają testy jednostkowe handlera.
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

        // 30 min meczu → decyduje 4 h bez ruchu.
        (await RunAsync(TimeSpan.FromHours(3.5))).Should().Be(0);
        (await RunAsync(TimeSpan.FromHours(4.5))).Should().Be(1);

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
        await s.UpdateSettingsAsync(matchMinutes: 120);
        await s.StartAsync();
        var id = await StartGroupMatchAsync(s, homeGoals: 0);

        // 120 min meczu → 3 × 120 min = 6 h, choć 4 h bez ruchu minęły wcześniej.
        (await RunAsync(TimeSpan.FromHours(5))).Should().Be(0);
        (await MatchAsync(s, id)).GetString("status").Should().Be("Live");

        (await RunAsync(TimeSpan.FromHours(6.5))).Should().Be(1);
        (await MatchAsync(s, id)).GetString("status").Should().Be("Finished");
    }

    [Fact]
    public async Task MNZ_042_KnockoutDraw_IsPausedOnce_AndTheOrganizerIsAskedToDecide()
    {
        var s = await TournamentScenario.KnockoutAsync(Factory);
        var id = (await s.ScheduleAsync()).First().GetGuid("id");
        var path = $"/api/tournaments/{s.TournamentId}/matches/{id}";
        (await s.Organizer.PostAsync($"{path}/start", null)).IsSuccessStatusCode.Should().BeTrue();

        (await RunAsync(TimeSpan.FromHours(5))).Should().Be(0);

        (await MatchAsync(s, id)).GetString("status").Should().Be("Paused");
        (await s.NotificationsAsync(s.Organizer)).Should().ContainSingle(n => n.GetString("type") == "MatchAwaitingDecision");

        // Organizer wznawia, żeby rozegrać dogrywkę: system już się nie wtrąca.
        (await s.Organizer.PostAsync($"{path}/resume", null)).IsSuccessStatusCode.Should().BeTrue();
        (await RunAsync(TimeSpan.FromHours(12))).Should().Be(0);

        (await MatchAsync(s, id)).GetString("status").Should().Be("Live");
        (await s.NotificationsAsync(s.Organizer)).Count(n => n.GetString("type") == "MatchAwaitingDecision").Should().Be(1);
    }
}

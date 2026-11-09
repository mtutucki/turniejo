using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Turniejo.IntegrationTests.Infrastructure;

namespace Turniejo.IntegrationTests.Rules;

/// <summary>Arkusze QA „Panele i powiadomienia” oraz „Strona publiczna”: reguły, które wyszły OK.</summary>
public sealed class PanelsAndPublicRulesTests(IntegrationTestWebApplicationFactory factory) : IntegrationTestBase(factory)
{
    private const int Goal = 5, OwnGoal = 6;

    private static string T(TournamentScenario s) => $"/api/tournaments/{s.TournamentId}";

    private static Task<HttpResponseMessage> PublishAsync(TournamentScenario s, bool isPublic) =>
        s.Organizer.PutAsJsonAsync($"{T(s)}/public-settings",
            new { isPublic, showTeams = true, showPlayers = true, showSchedule = true, showStandings = true, showBracket = true, showLiveMatches = true, showStats = true });

    private static async Task GoalAsync(TournamentScenario s, Guid match, string team, Guid player, int type = Goal) =>
        (await s.Organizer.PostAsJsonAsync($"{T(s)}/matches/{match}/goals",
            new { tournamentTeamId = s.Team(team), playerId = player, assistPlayerId = (Guid?)null, goalType = type, minute = 10, second = (int?)null }))
        .IsSuccessStatusCode.Should().BeTrue();

    [Fact]
    public async Task PAN_002_CoachPanel_MatchesTheOrganizersData()
    {
        var s = await TournamentScenario.GroupsAsync(Factory, teamsPerGroup: 3);
        var (coach, coachId) = await s.JoinAsync("Coach");
        await s.AssignCoachAsync("T1", coachId);
        var (_, adam) = await s.CreatePlayerAsync("T1", "Adam", "Strzelec");
        var match = (await s.MatchBetweenAsync("T1", "T2")).GetGuid("id");
        (await s.Organizer.PostAsync($"{T(s)}/matches/{match}/start", null)).IsSuccessStatusCode.Should().BeTrue();
        await GoalAsync(s, match, "T1", adam);
        (await s.Organizer.PostAsJsonAsync($"{T(s)}/matches/{match}/finish", new { resultType = 2, winnerTournamentTeamId = s.Team("T1") }))
            .IsSuccessStatusCode.Should().BeTrue();
        await FlushOutboxAsync();
        var team = $"/api/coach/teams/{s.Team("T1")}";

        (await (await coach.GetAsync($"{team}/players")).ReadJsonAsync()).EnumerateArray().Should().ContainSingle(p => p.GetGuid("playerId") == adam);
        (await (await coach.GetAsync($"{team}/schedule")).ReadJsonAsync()).EnumerateArray().Should().HaveCount(1);
        (await (await coach.GetAsync($"{team}/results")).ReadJsonAsync()).EnumerateArray().Should().HaveCount(1);
        (await (await coach.GetAsync($"{team}/player-stats")).ReadJsonAsync()).EnumerateArray()
            .Single(p => p.GetGuid("playerId") == adam).GetInt("goals").Should().Be(1);
    }

    [Fact]
    public async Task PAN_009_Announcement_ReachesEveryParticipant()
    {
        var s = await TournamentScenario.GroupsAsync(Factory, teamsPerGroup: 2);
        var (coach, coachId) = await s.JoinAsync("Coach");
        await s.AssignCoachAsync("T1", coachId);
        var (referee, _) = await s.JoinAsync("Referee");
        var (_, child) = await s.CreatePlayerAsync("T2", "Ola", "Dziecko");
        var (parent, parentId) = await s.NewUserAsync("parent");
        await s.AddGuardianAsync(child, "T2", parentId);

        (await s.Organizer.PostAsJsonAsync($"{T(s)}/announcements", new { title = "Zmiana", message = "Start o 10:30" })).IsSuccessStatusCode.Should().BeTrue();

        foreach (var participant in new[] { coach, referee, parent })
            (await s.NotificationsAsync(participant)).Should().Contain(n => n.GetString("type") == "OrganizerAnnouncement");
    }

    [Fact]
    public async Task PAN_011_OnlyReadNotifications_CanBeDeleted()
    {
        var s = await TournamentScenario.GroupsAsync(Factory, teamsPerGroup: 2);
        var (referee, _) = await s.JoinAsync("Referee");
        (await s.Organizer.PostAsJsonAsync($"{T(s)}/announcements", new { title = "1", message = "Pierwsze" })).IsSuccessStatusCode.Should().BeTrue();
        var id = (await s.NotificationsAsync(referee)).Single().GetGuid("notificationRecipientId");

        (await referee.DeleteAsync($"/api/notifications/{id}")).IsSuccessStatusCode.Should().BeFalse();
        (await referee.PatchAsync($"/api/notifications/{id}/read", null)).IsSuccessStatusCode.Should().BeTrue();
        (await s.NotificationsAsync(referee)).Single().GetString("status").Should().Be("Read");
        (await referee.DeleteAsync($"/api/notifications/{id}")).IsSuccessStatusCode.Should().BeTrue();
        (await s.NotificationsAsync(referee)).Should().BeEmpty();
    }

    [Fact]
    public async Task PAN_012_GeneratingTheSchedule_SendsNoTermChangeNotifications()
    {
        var s = await TournamentScenario.GroupsAsync(Factory, teamsPerGroup: 4, schedule: false);
        var (coach, coachId) = await s.JoinAsync("Coach");
        await s.AssignCoachAsync("T1", coachId);

        await s.GenerateScheduleAsync();

        (await s.NotificationsAsync(coach)).Should().NotContain(n =>
            n.GetString("type") == "MatchTimeChanged" || n.GetString("type") == "ScheduleChanged" || n.GetString("type") == "PitchChanged");
    }

    [Fact]
    public async Task PUB_001_PUB_002_PublicPage_IsAvailableOnlyWhenPublished()
    {
        var s = await TournamentScenario.GroupsAsync(Factory, teamsPerGroup: 2);
        var slug = (await s.TournamentAsync()).GetString("slug");
        var anonymous = CreateClient();

        (await PublishAsync(s, false)).IsSuccessStatusCode.Should().BeTrue();
        (await anonymous.GetAsync($"/public/tournaments/{slug}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await anonymous.GetAsync($"/public/tournaments/{slug}/schedule")).IsSuccessStatusCode.Should().BeFalse();

        (await PublishAsync(s, true)).IsSuccessStatusCode.Should().BeTrue();
        (await anonymous.GetAsync($"/public/tournaments/{slug}")).IsSuccessStatusCode.Should().BeTrue();
        (await anonymous.GetAsync($"/public/tournaments/{slug}/schedule")).IsSuccessStatusCode.Should().BeTrue();
    }

    [Fact]
    public async Task PUB_008_PublicStats_FollowTheEvents_AndOwnGoalsDoNotCount()
    {
        var s = await TournamentScenario.GroupsAsync(Factory, teamsPerGroup: 2);
        var (_, adam) = await s.CreatePlayerAsync("T1", "Adam", "Strzelec");
        var (_, cezary) = await s.CreatePlayerAsync("T2", "Cezary", "Pechowiec");
        var match = (await s.MatchBetweenAsync("T1", "T2")).GetGuid("id");
        (await s.Organizer.PostAsync($"{T(s)}/matches/{match}/start", null)).IsSuccessStatusCode.Should().BeTrue();
        await GoalAsync(s, match, "T1", adam);
        await GoalAsync(s, match, "T1", adam);
        await GoalAsync(s, match, "T2", cezary, OwnGoal);
        await FlushOutboxAsync();
        (await PublishAsync(s, true)).IsSuccessStatusCode.Should().BeTrue();
        var slug = (await s.TournamentAsync()).GetString("slug");

        var scorers = (await (await CreateClient().GetAsync($"/public/tournaments/{slug}/stats/top-scorers")).ReadJsonAsync()).EnumerateArray().ToList();

        scorers.Single(r => r.GetGuid("playerId") == adam).GetInt("count").Should().Be(2);
        scorers.Should().NotContain(r => r.GetGuid("playerId") == cezary);
    }
}

using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Turniejo.IntegrationTests.Infrastructure;
using Turniejo.Tournaments.Domain.Enums;

namespace Turniejo.IntegrationTests.Rules;

/// <summary>Arkusze QA „Panele i powiadomienia” oraz „Strona publiczna”: reguły, które wyszły OK.</summary>
public sealed class PanelsAndPublicRulesTests(IntegrationTestWebApplicationFactory factory) : IntegrationTestBase(factory)
{
    private const int Goal = 5, OwnGoal = 6;

    private static string T(TournamentScenario s) => $"/api/tournaments/{s.TournamentId}";

    private static Task<HttpResponseMessage> PublishAsync(TournamentScenario s, bool isPublic, bool showPlayers = true, bool abbreviatePlayerNames = true, bool showInDirectory = true) =>
        s.Organizer.PutAsJsonAsync($"{T(s)}/public-settings",
            new { isPublic, showTeams = true, showPlayers, showSchedule = true, showStandings = true, showBracket = true, showLiveMatches = true, showStats = true, abbreviatePlayerNames, showInDirectory });

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
    public async Task PAN_013_TournamentStats_ShowScorersAssistsAndMvp()
    {
        var s = await TournamentScenario.GroupsAsync(Factory, teamsPerGroup: 2);
        var (_, adam) = await s.CreatePlayerAsync("T1", "Adam", "Strzelec");
        var (_, bartek) = await s.CreatePlayerAsync("T1", "Bartek", "Podający");
        var match = (await s.MatchBetweenAsync("T1", "T2")).GetGuid("id");
        var m = $"{T(s)}/matches/{match}";
        (await s.Organizer.PostAsync($"{m}/start", null)).IsSuccessStatusCode.Should().BeTrue();
        (await s.Organizer.PostAsJsonAsync($"{m}/goals",
                new { tournamentTeamId = s.Team("T1"), playerId = adam, assistPlayerId = bartek, goalType = Goal, minute = 10, second = (int?)null }))
            .IsSuccessStatusCode.Should().BeTrue();
        (await s.Organizer.PostAsJsonAsync($"{m}/finish", new { resultType = 2, winnerTournamentTeamId = s.Team("T1") })).IsSuccessStatusCode.Should().BeTrue();
        var mvp = await s.Organizer.PostAsJsonAsync($"{m}/mvp", new { tournamentTeamId = s.Team("T1"), playerId = adam });
        mvp.IsSuccessStatusCode.Should().BeTrue(await mvp.Content.ReadAsStringAsync());
        await FlushOutboxAsync();

        async Task<int> CountAsync(string kind, Guid player) =>
            (await (await s.Organizer.GetAsync($"{T(s)}/stats/{kind}")).ReadJsonAsync()).EnumerateArray()
                .Where(r => r.GetGuid("playerId") == player).Select(r => r.GetInt("count")).FirstOrDefault();

        (await CountAsync("top-scorers", adam)).Should().Be(1);
        (await CountAsync("top-assists", bartek)).Should().Be(1);
        (await CountAsync("mvp", adam)).Should().Be(1);
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
    public async Task PUB_020_PublicList_ShowsPublishedTournaments_WithOrganizerTeamsAndLiveMatches()
    {
        var live = await TournamentScenario.GroupsAsync(Factory, teamsPerGroup: 2);
        (await PublishAsync(live, true)).IsSuccessStatusCode.Should().BeTrue();
        var match = (await live.MatchBetweenAsync("T1", "T2")).GetGuid("id");
        (await live.Organizer.PostAsync($"{T(live)}/matches/{match}/start", null)).IsSuccessStatusCode.Should().BeTrue();

        var hidden = await TournamentScenario.GroupsAsync(Factory, teamsPerGroup: 2, schedule: false);
        var cancelled = await TournamentScenario.GroupsAsync(Factory, teamsPerGroup: 2, schedule: false);
        (await PublishAsync(cancelled, true)).IsSuccessStatusCode.Should().BeTrue();
        await TournamentFlow.ChangeStatusAsync(cancelled.Organizer, cancelled.TournamentId, TournamentStatus.Cancelled);
        var draft = await TournamentScenario.GroupsAsync(Factory, teamsPerGroup: 1, schedule: false);
        await TournamentFlow.ChangeStatusAsync(draft.Organizer, draft.TournamentId, TournamentStatus.RegistrationOpen);
        await TournamentFlow.ChangeStatusAsync(draft.Organizer, draft.TournamentId, TournamentStatus.Draft);
        (await PublishAsync(draft, true)).IsSuccessStatusCode.Should().BeTrue();

        async Task<string> SlugAsync(TournamentScenario s) => (await s.TournamentAsync()).GetString("slug");
        var list = (await (await CreateClient().GetAsync("/public/tournaments")).ReadJsonAsync()).EnumerateArray().ToList();
        var slugs = list.Select(t => t.GetString("slug")).ToList();

        var liveSlug = await SlugAsync(live);
        var item = list.Single(t => t.GetString("slug") == liveSlug);
        item.GetString("status").Should().Be("InProgress");
        item.GetProperty("organizer").GetString("name").Should().StartWith("Org ");
        item.GetProperty("categories").EnumerateArray().Select(c => c.GetString()).Should().Equal("Open");
        item.GetInt("teamCount").Should().Be(2);
        item.GetInt("liveMatchCount").Should().Be(1);
        slugs.Should().NotContain([await SlugAsync(hidden), await SlugAsync(cancelled), await SlugAsync(draft)]);
    }

    [Fact]
    public async Task PUB_023_TournamentHiddenFromTheList_StillHasItsPortal()
    {
        var s = await TournamentScenario.GroupsAsync(Factory, teamsPerGroup: 2);
        (await PublishAsync(s, true, showInDirectory: false)).IsSuccessStatusCode.Should().BeTrue();
        var slug = (await s.TournamentAsync()).GetString("slug");
        var anonymous = CreateClient();

        var list = (await (await anonymous.GetAsync("/public/tournaments")).ReadJsonAsync()).EnumerateArray();
        list.Select(t => t.GetString("slug")).Should().NotContain(slug);
        (await anonymous.GetAsync($"/public/tournaments/{slug}")).IsSuccessStatusCode.Should().BeTrue();
        (await (await s.Organizer.GetAsync($"{T(s)}/public-settings")).ReadJsonAsync())
            .GetProperty("showInDirectory").GetBoolean().Should().BeFalse();
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

    private async Task<(Guid Match, Guid Adam, string Slug)> PublicMatchWithGoalAsync(TournamentScenario s)
    {
        var (_, adam) = await s.CreatePlayerAsync("T1", "Adam", "Strzelec");
        var match = (await s.MatchBetweenAsync("T1", "T2")).GetGuid("id");
        (await s.Organizer.PostAsync($"{T(s)}/matches/{match}/start", null)).IsSuccessStatusCode.Should().BeTrue();
        await GoalAsync(s, match, "T1", adam);
        await FlushOutboxAsync();
        return (match, adam, (await s.TournamentAsync()).GetString("slug"));
    }

    private static string LiveTeamsJson(System.Text.Json.JsonElement live) =>
        live.GetProperty("homeTeam").GetRawText() + live.GetProperty("awayTeam").GetRawText();

    [Fact]
    public async Task PUB_004_AbbreviatedNames_HideFullSurnamesOnThePortal()
    {
        var s = await TournamentScenario.GroupsAsync(Factory, teamsPerGroup: 2);
        var (match, _, slug) = await PublicMatchWithGoalAsync(s);
        var anonymous = CreateClient();

        (await PublishAsync(s, true, abbreviatePlayerNames: true)).IsSuccessStatusCode.Should().BeTrue();
        var live = await (await anonymous.GetAsync($"/public/matches/{match}/live")).ReadJsonAsync();
        var scorers = await (await anonymous.GetAsync($"/public/tournaments/{slug}/stats/top-scorers")).Content.ReadAsStringAsync();
        LiveTeamsJson(live).Should().NotContain("Strzelec");
        scorers.Should().Contain("Adam").And.NotContain("Strzelec");

        (await PublishAsync(s, true, abbreviatePlayerNames: false)).IsSuccessStatusCode.Should().BeTrue();
        live = await (await anonymous.GetAsync($"/public/matches/{match}/live")).ReadJsonAsync();
        LiveTeamsJson(live).Should().Contain("Strzelec");
    }

    [Fact]
    public async Task PUB_003b_HiddenPlayers_AreNotInLiveSquadsOrProfiles()
    {
        var s = await TournamentScenario.GroupsAsync(Factory, teamsPerGroup: 2);
        var (match, adam, _) = await PublicMatchWithGoalAsync(s);
        var anonymous = CreateClient();

        (await PublishAsync(s, true, showPlayers: false)).IsSuccessStatusCode.Should().BeTrue();

        var live = await (await anonymous.GetAsync($"/public/matches/{match}/live")).ReadJsonAsync();
        live.GetProperty("homeTeam").GetProperty("players").EnumerateArray().Should().BeEmpty();
        live.GetProperty("awayTeam").GetProperty("players").EnumerateArray().Should().BeEmpty();
        (await anonymous.GetAsync($"/public/players/{adam}")).IsSuccessStatusCode.Should().BeFalse();
    }

    [Fact]
    public async Task PUB_007_PublicMatch_ShowsMvp_AndHidesRevertedEvents()
    {
        var s = await TournamentScenario.GroupsAsync(Factory, teamsPerGroup: 2);
        var (match, adam, _) = await PublicMatchWithGoalAsync(s);
        var m = $"{T(s)}/matches/{match}";
        var reverted = await s.Organizer.PostAsJsonAsync($"{m}/goals",
            new { tournamentTeamId = s.Team("T1"), playerId = adam, assistPlayerId = (Guid?)null, goalType = Goal, minute = 20, second = (int?)null });
        var revertedId = (await reverted.ReadJsonAsync()).GetGuid("matchEventId");
        (await s.Organizer.DeleteAsync($"{m}/events/{revertedId}")).IsSuccessStatusCode.Should().BeTrue();
        (await s.Organizer.PostAsJsonAsync($"{m}/finish", new { resultType = 2, winnerTournamentTeamId = s.Team("T1") })).IsSuccessStatusCode.Should().BeTrue();
        (await s.Organizer.PostAsJsonAsync($"{m}/mvp", new { tournamentTeamId = s.Team("T1"), playerId = adam })).IsSuccessStatusCode.Should().BeTrue();
        (await PublishAsync(s, true)).IsSuccessStatusCode.Should().BeTrue();

        var live = await (await CreateClient().GetAsync($"/public/matches/{match}/live")).ReadJsonAsync();

        var events = live.GetProperty("events").EnumerateArray().ToList();
        events.Should().Contain(e => e.GetString("type") == "MvpSelected" && e.GetGuidOrNull("playerId") == adam);
        events.Should().NotContain(e => e.GetGuid("id") == revertedId);
        events.Count(e => e.GetString("type") == "Goal").Should().Be(1);
    }

    [Fact]
    public async Task PAN_007_StartAndEndOfAMatch_NotifyCoachesAndParents()
    {
        var s = await TournamentScenario.GroupsAsync(Factory, teamsPerGroup: 2);
        var (coach, coachId) = await s.JoinAsync("Coach");
        await s.AssignCoachAsync("T1", coachId);
        var (_, child) = await s.CreatePlayerAsync("T2", "Ola", "Dziecko");
        var (parent, parentId) = await s.NewUserAsync("parent");
        await s.AddGuardianAsync(child, "T2", parentId);
        var match = (await s.MatchBetweenAsync("T1", "T2")).GetGuid("id");

        await s.PlayAsync(match, 1, 0);

        foreach (var participant in new[] { coach, parent })
        {
            var types = (await s.NotificationsAsync(participant)).Select(n => n.GetString("type")).ToList();
            types.Should().Contain("MatchStarted").And.Contain("MatchFinished");
        }
    }
}

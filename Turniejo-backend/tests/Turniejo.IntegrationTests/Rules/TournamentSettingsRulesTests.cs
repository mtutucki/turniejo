using System.Net.Http.Json;
using FluentAssertions;
using Turniejo.IntegrationTests.Infrastructure;
using Turniejo.Tournaments.Domain.Enums;

namespace Turniejo.IntegrationTests.Rules;

/// <summary>Arkusz QA „Ustawienia turnieju”: reguły, które w ręcznym przebiegu wyszły OK.</summary>
public sealed class TournamentSettingsRulesTests(IntegrationTestWebApplicationFactory factory) : IntegrationTestBase(factory)
{
    private static string T(TournamentScenario s) => $"/api/tournaments/{s.TournamentId}";

    private static Task<HttpResponseMessage> UpdateDetailsAsync(TournamentScenario s, int teamLimit = 32, string name = "Cup") =>
        s.Organizer.PutAsJsonAsync(T(s), new { name, startAt = s.StartsAt, endAt = s.StartsAt.AddDays(1), teamLimit, description = (string?)null });

    private static async Task<string> StatusAsync(TournamentScenario s) =>
        (await s.TournamentAsync()).GetString("status");

    [Fact]
    public async Task UST_005_LimitBelowAcceptedTeams_IsRejected()
    {
        var s = await TournamentScenario.GroupsAsync(Factory, teamsPerGroup: 4, schedule: false);

        (await UpdateDetailsAsync(s, teamLimit: 3)).IsSuccessStatusCode.Should().BeFalse();
        (await UpdateDetailsAsync(s, teamLimit: 4)).IsSuccessStatusCode.Should().BeTrue();
    }

    [Fact]
    public async Task UST_008_SportSettings_AreLockedWhileTheTournamentIsInProgress()
    {
        var s = await TournamentScenario.GroupsAsync(Factory, teamsPerGroup: 2);

        var response = await s.Organizer.PutAsJsonAsync($"{T(s)}/settings",
            new { pointsForWin = 2, pointsForDraw = 1, pointsForLoss = 0, matchDurationMinutes = 30, breakDurationMinutes = 5, isThirdPlaceMatchEnabled = false, refereeEditWindowMinutes = 60, walkoverHomeScore = 3, walkoverAwayScore = 0, canCoachEditSquadAfterStart = false });

        response.IsSuccessStatusCode.Should().BeFalse();
        (await (await s.Organizer.GetAsync($"{T(s)}/settings")).ReadJsonAsync()).GetInt("pointsForWin").Should().Be(3);
    }

    [Fact]
    public async Task UST_009_PitchWithoutMatches_CanBeDeleted_PitchWithMatches_OnlyDeactivated()
    {
        var s = await TournamentScenario.GroupsAsync(Factory, teamsPerGroup: 2);
        var spare = await TournamentFlow.AddPitchAsync(s.Organizer, s.TournamentId, "Zapasowe");

        (await s.Organizer.DeleteAsync($"{T(s)}/pitches/{spare}")).IsSuccessStatusCode.Should().BeTrue();
        (await s.Organizer.DeleteAsync($"{T(s)}/pitches/{s.PitchId}")).IsSuccessStatusCode.Should().BeFalse();

        var second = await TournamentFlow.AddPitchAsync(s.Organizer, s.TournamentId, "Boczne");
        (await s.Organizer.PutAsJsonAsync($"{T(s)}/pitches/{second}", new { name = "Boczne", description = (string?)null, isActive = false }))
            .IsSuccessStatusCode.Should().BeTrue();
        var match = (await s.MatchBetweenAsync("T1", "T2")).GetGuid("id");
        (await s.Organizer.PatchAsJsonAsync($"{T(s)}/matches/{match}/pitch", new { pitchId = second }))
            .IsSuccessStatusCode.Should().BeFalse();
    }

    [Fact]
    public async Task UST_011_TeamsCanBeRegisteredInDraftAndAfterRegistrationClosed()
    {
        var s = await TournamentScenario.GroupsAsync(Factory, teamsPerGroup: 2, schedule: false);
        (await StatusAsync(s)).Should().Be("RegistrationClosed");
        var closed = await TournamentFlow.CreateTeamAsync(s.Organizer, s.OrganizationId, "Po zamknięciu");
        await TournamentFlow.AddTeamToTournamentAsync(s.Organizer, s.TournamentId, closed, s.CategoryId);

        await TournamentFlow.ChangeStatusAsync(s.Organizer, s.TournamentId, TournamentStatus.RegistrationOpen);
        await TournamentFlow.ChangeStatusAsync(s.Organizer, s.TournamentId, TournamentStatus.Draft);
        var draft = await TournamentFlow.CreateTeamAsync(s.Organizer, s.OrganizationId, "W szkicu");
        await TournamentFlow.AddTeamToTournamentAsync(s.Organizer, s.TournamentId, draft, s.CategoryId);

        var teams = (await (await s.Organizer.GetAsync($"{T(s)}/teams")).ReadJsonAsync()).EnumerateArray().ToList();
        teams.Where(t => t.GetGuid("teamId") == closed || t.GetGuid("teamId") == draft)
            .Should().HaveCount(2).And.OnlyContain(t => t.GetString("registrationStatus") == "Pending");
    }

    [Theory]
    [InlineData(TournamentStatus.Draft)]
    [InlineData(TournamentStatus.RegistrationOpen)]
    [InlineData(TournamentStatus.InProgress)]
    public async Task UST_012_TournamentCanBeCancelledAtEveryStage_AndIsThenLocked(TournamentStatus stage)
    {
        var s = stage == TournamentStatus.InProgress
            ? await TournamentScenario.GroupsAsync(Factory, teamsPerGroup: 2)
            : await TournamentScenario.GroupsAsync(Factory, teamsPerGroup: 2, schedule: false);
        if (stage != TournamentStatus.InProgress)
        {
            await TournamentFlow.ChangeStatusAsync(s.Organizer, s.TournamentId, TournamentStatus.RegistrationOpen);
            if (stage == TournamentStatus.Draft)
                await TournamentFlow.ChangeStatusAsync(s.Organizer, s.TournamentId, TournamentStatus.Draft);
        }

        await TournamentFlow.ChangeStatusAsync(s.Organizer, s.TournamentId, TournamentStatus.Cancelled);

        (await StatusAsync(s)).Should().Be("Cancelled");
        (await UpdateDetailsAsync(s, name: "Po anulowaniu")).IsSuccessStatusCode.Should().BeFalse();
        (await s.Organizer.PatchAsJsonAsync($"{T(s)}/status", new { tournamentStatus = (int)TournamentStatus.RegistrationOpen }))
            .IsSuccessStatusCode.Should().BeFalse();
    }

    [Fact]
    public async Task UST_013_DashboardCountsTeamsByStatus()
    {
        var s = await TournamentScenario.GroupsAsync(Factory, teamsPerGroup: 3, schedule: false);
        var rejectedTeam = await TournamentFlow.CreateTeamAsync(s.Organizer, s.OrganizationId, "Odrzuceni");
        var rejected = await TournamentFlow.AddTeamToTournamentAsync(s.Organizer, s.TournamentId, rejectedTeam, s.CategoryId);
        (await s.Organizer.PatchAsJsonAsync($"{T(s)}/tournament-teams/{rejected}/registration-status", new { registrationStatus = 3 }))
            .IsSuccessStatusCode.Should().BeTrue();
        (await s.Organizer.PatchAsJsonAsync($"{T(s)}/tournament-teams/{s.Team("T1")}/registration-status", new { registrationStatus = 4 }))
            .IsSuccessStatusCode.Should().BeTrue();
        var pendingTeam = await TournamentFlow.CreateTeamAsync(s.Organizer, s.OrganizationId, "Oczekujący");
        await TournamentFlow.AddTeamToTournamentAsync(s.Organizer, s.TournamentId, pendingTeam, s.CategoryId);

        var teams = (await (await s.Organizer.GetAsync($"{T(s)}/dashboard")).ReadJsonAsync()).GetProperty("teams");

        teams.GetInt("accepted").Should().Be(2);
        teams.GetInt("pending").Should().Be(1);
        teams.GetInt("rejected").Should().Be(1);
        teams.GetInt("withdrawn").Should().Be(1);
    }

    // QA v3: UST-015 - dwóch organizatorów zapisuje naraz: drugi zapis bez odświeżenia nadpisywał pierwszy.
    // Zapis z nieaktualnymi wartościami (wczytanymi przed cudzą zmianą) dostaje konflikt.
    [Fact]
    public async Task UST_015_StaleRulesSave_IsRejected_FreshOneGoesThrough()
    {
        var s = await TournamentScenario.GroupsAsync(Factory, teamsPerGroup: 2, schedule: false);
        var loaded = await (await s.Organizer.GetAsync($"{T(s)}/settings")).ReadJsonAsync();
        object Rules(int win, object? expected) => new
        {
            pointsForWin = win, pointsForDraw = 1, pointsForLoss = 0, matchDurationMinutes = 30, breakDurationMinutes = 5,
            isThirdPlaceMatchEnabled = false, refereeEditWindowMinutes = 60, walkoverHomeScore = 3, walkoverAwayScore = 0,
            canCoachEditSquadAfterStart = false, expected,
        };
        var expected = System.Text.Json.JsonSerializer.Deserialize<object>(loaded.GetRawText());

        (await s.Organizer.PutAsJsonAsync($"{T(s)}/settings", Rules(4, expected))).IsSuccessStatusCode.Should().BeTrue();

        var stale = await s.Organizer.PutAsJsonAsync($"{T(s)}/settings", Rules(5, expected));
        stale.StatusCode.Should().Be(System.Net.HttpStatusCode.Conflict);
        (await (await s.Organizer.GetAsync($"{T(s)}/settings")).ReadJsonAsync()).GetInt("pointsForWin").Should().Be(4);

        var fresh = System.Text.Json.JsonSerializer.Deserialize<object>((await (await s.Organizer.GetAsync($"{T(s)}/settings")).ReadJsonAsync()).GetRawText());
        (await s.Organizer.PutAsJsonAsync($"{T(s)}/settings", Rules(5, fresh))).IsSuccessStatusCode.Should().BeTrue();
    }

    [Fact]
    public async Task UST_015_StalePortalSave_IsRejected()
    {
        var s = await TournamentScenario.GroupsAsync(Factory, teamsPerGroup: 2, schedule: false);
        var loaded = System.Text.Json.JsonSerializer.Deserialize<object>(
            (await (await s.Organizer.GetAsync($"{T(s)}/public-settings")).ReadJsonAsync()).GetRawText());
        object Portal(bool isPublic, bool showStats) => new
        {
            isPublic, showTeams = true, showPlayers = true, showSchedule = true, showStandings = true, showBracket = true,
            showLiveMatches = true, showStats, abbreviatePlayerNames = true, expected = loaded,
        };

        (await s.Organizer.PutAsJsonAsync($"{T(s)}/public-settings", Portal(true, true))).IsSuccessStatusCode.Should().BeTrue();

        (await s.Organizer.PutAsJsonAsync($"{T(s)}/public-settings", Portal(false, false))).StatusCode
            .Should().Be(System.Net.HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task UST_002_TwoTournamentsWithTheSameName_GetSeparatePublicPages()
    {
        var s = await TournamentScenario.GroupsAsync(Factory, teamsPerGroup: 2, schedule: false);
        var first = await s.TournamentAsync();
        var created = await s.Organizer.PostAsJsonAsync("/api/tournaments", new
        {
            organizationId = s.OrganizationId, name = first.GetString("name"), startAt = s.StartsAt, endAt = s.StartsAt.AddDays(1), teamLimit = 8, description = (string?)null,
        });
        created.IsSuccessStatusCode.Should().BeTrue(await created.Content.ReadAsStringAsync());
        var second = await created.ReadJsonAsync();
        var publicSettings = new { isPublic = true, showTeams = true, showPlayers = true, showSchedule = true, showStandings = true, showBracket = true, showLiveMatches = true, showStats = true };
        (await s.Organizer.PutAsJsonAsync($"{T(s)}/public-settings", publicSettings)).IsSuccessStatusCode.Should().BeTrue();
        (await s.Organizer.PutAsJsonAsync($"/api/tournaments/{second.GetGuid("tournamentId")}/public-settings", publicSettings)).IsSuccessStatusCode.Should().BeTrue();

        second.GetString("slug").Should().NotBe(first.GetString("slug"));
        var anonymous = CreateClient();
        (await (await anonymous.GetAsync($"/public/tournaments/{first.GetString("slug")}")).ReadJsonAsync()).GetGuid("id").Should().Be(s.TournamentId);
        (await (await anonymous.GetAsync($"/public/tournaments/{second.GetString("slug")}")).ReadJsonAsync()).GetGuid("id").Should().Be(second.GetGuid("tournamentId"));
    }
}

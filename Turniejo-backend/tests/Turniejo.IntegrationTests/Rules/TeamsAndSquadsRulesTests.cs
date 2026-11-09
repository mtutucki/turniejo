using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Turniejo.IntegrationTests.Infrastructure;

namespace Turniejo.IntegrationTests.Rules;

/// <summary>Arkusz QA „Drużyny i składy”: reguły, które w ręcznym przebiegu wyszły OK.</summary>
public sealed class TeamsAndSquadsRulesTests(IntegrationTestWebApplicationFactory factory) : IntegrationTestBase(factory)
{
    private const int Pending = 1, Accepted = 2, Rejected = 3, Withdrawn = 4;
    private const int Unpaid = 2, Paid = 3, Refunded = 4;

    private static string T(TournamentScenario s) => $"/api/tournaments/{s.TournamentId}";

    private static async Task<List<JsonElement>> TeamsAsync(TournamentScenario s) =>
        (await (await s.Organizer.GetAsync($"{T(s)}/teams")).ReadJsonAsync()).EnumerateArray().ToList();

    private static async Task<JsonElement> EntryAsync(TournamentScenario s, Guid tournamentTeamId) =>
        (await TeamsAsync(s)).Single(t => t.GetGuid("id") == tournamentTeamId);

    private static Task<HttpResponseMessage> SetStatusAsync(TournamentScenario s, Guid tournamentTeamId, int status) =>
        s.Organizer.PatchAsJsonAsync($"{T(s)}/tournament-teams/{tournamentTeamId}/registration-status", new { registrationStatus = status });

    /// <summary>Nowa drużyna klubu zgłoszona do turnieju (bez akceptacji).</summary>
    private static async Task<(Guid ClubTeamId, Guid TournamentTeamId)> RegisterTeamAsync(TournamentScenario s, string name, Guid? categoryId = null)
    {
        var teamId = await TournamentFlow.CreateTeamAsync(s.Organizer, s.OrganizationId, name);
        var response = await s.Organizer.PostAsJsonAsync($"{T(s)}/teams", new { teamId, categoryId = categoryId ?? s.CategoryId });
        response.IsSuccessStatusCode.Should().BeTrue(await response.Content.ReadAsStringAsync());
        return (teamId, (await response.ReadJsonAsync()).GetGuid("tournamentTeamId"));
    }

    private static async Task<List<JsonElement>> SquadAsync(TournamentScenario s, string team) =>
        (await (await s.Organizer.GetAsync($"{T(s)}/tournament-teams/{s.Team(team)}/players")).ReadJsonAsync()).EnumerateArray().ToList();

    private static async Task<List<JsonElement>> RosterAsync(HttpClient client, Guid clubTeamId) =>
        (await (await client.GetAsync($"/api/teams/{clubTeamId}/players")).ReadJsonAsync()).GetProperty("players").EnumerateArray().ToList();

    private static Task<HttpResponseMessage> AddToRosterAsync(HttpClient client, Guid clubTeamId, string first, string last, int? shirt, bool allowDuplicate = false) =>
        client.PostAsJsonAsync($"/api/teams/{clubTeamId}/players",
            new { firstName = first, lastName = last, birthDate = new DateTime(2012, 3, 3), defaultPosition = 3, shirtNumber = shirt, allowDuplicate });

    private static async Task<List<JsonElement>> CoachTeamsAsync(HttpClient coach) =>
        (await (await coach.GetAsync("/api/coach/teams")).ReadJsonAsync()).EnumerateArray().ToList();

    [Fact]
    public async Task DRU_003_RegisteredTeam_StartsAsPending()
    {
        var s = await TournamentScenario.GroupsAsync(Factory, teamsPerGroup: 2, schedule: false);

        var (_, tt) = await RegisterTeamAsync(s, "Nowi");

        (await EntryAsync(s, tt)).GetString("registrationStatus").Should().Be("Pending");
    }

    [Fact]
    public async Task DRU_004_SameTeamTwiceInTheSameCategory_IsRejected()
    {
        var s = await TournamentScenario.GroupsAsync(Factory, teamsPerGroup: 2, schedule: false);

        var again = await s.Organizer.PostAsJsonAsync($"{T(s)}/teams", new { teamId = s.ClubTeam("T1"), categoryId = s.CategoryId });

        again.IsSuccessStatusCode.Should().BeFalse();
        (await TeamsAsync(s)).Count(t => t.GetGuid("teamId") == s.ClubTeam("T1")).Should().Be(1);
    }

    [Fact]
    public async Task DRU_005_DRU_008_AcceptingAboveTheLimit_IsRejected_UntilAPlaceIsFreed()
    {
        var s = await TournamentScenario.GroupsAsync(Factory, teamsPerGroup: 3, schedule: false);
        (await s.Organizer.PutAsJsonAsync(T(s), new { name = "Cup", startAt = s.StartsAt, endAt = s.StartsAt.AddDays(1), teamLimit = 4, description = (string?)null }))
            .IsSuccessStatusCode.Should().BeTrue();
        var (_, fourth) = await RegisterTeamAsync(s, "Czwarta");
        var (_, fifth) = await RegisterTeamAsync(s, "Piąta");

        (await SetStatusAsync(s, fourth, Accepted)).IsSuccessStatusCode.Should().BeTrue();
        (await SetStatusAsync(s, fifth, Accepted)).IsSuccessStatusCode.Should().BeFalse();

        // Wycofanie zwalnia miejsce, a wycofaną drużynę da się zgłosić ponownie (wraca jako oczekująca).
        (await SetStatusAsync(s, s.Team("T1"), Withdrawn)).IsSuccessStatusCode.Should().BeTrue();
        (await EntryAsync(s, s.Team("T1"))).GetString("registrationStatus").Should().Be("Withdrawn");
        (await s.Organizer.PostAsJsonAsync($"{T(s)}/teams", new { teamId = s.ClubTeam("T1"), categoryId = s.CategoryId }))
            .IsSuccessStatusCode.Should().BeTrue();
        (await EntryAsync(s, s.Team("T1"))).GetString("registrationStatus").Should().Be("Pending");
        (await SetStatusAsync(s, fifth, Accepted)).IsSuccessStatusCode.Should().BeTrue();
    }

    [Fact]
    public async Task DRU_009_PaymentStatusChanges_AndUnpaidTeamCanBeAccepted()
    {
        var s = await TournamentScenario.GroupsAsync(Factory, teamsPerGroup: 2, schedule: false);
        var (_, tt) = await RegisterTeamAsync(s, "Płacący");
        var path = $"{T(s)}/tournament-teams/{tt}/payment-status";

        foreach (var (status, name) in new[] { (Unpaid, "Unpaid"), (Paid, "Paid"), (Refunded, "Refunded") })
        {
            (await s.Organizer.PatchAsJsonAsync(path, new { paymentStatus = status })).IsSuccessStatusCode.Should().BeTrue();
            (await EntryAsync(s, tt)).GetString("paymentStatus").Should().Be(name);
        }

        (await s.Organizer.PatchAsJsonAsync(path, new { paymentStatus = Unpaid })).IsSuccessStatusCode.Should().BeTrue();
        (await SetStatusAsync(s, tt, Accepted)).IsSuccessStatusCode.Should().BeTrue();
    }

    [Fact]
    public async Task DRU_011_NewSquadPlayer_LandsInTheSquadAndTheClubRoster()
    {
        var s = await TournamentScenario.GroupsAsync(Factory, teamsPerGroup: 2, schedule: false);

        var (_, playerId) = await s.CreatePlayerAsync("T1", "Jan", "Nowy", shirtNumber: 10);

        (await SquadAsync(s, "T1")).Should().Contain(p => p.GetGuid("playerId") == playerId);
        (await RosterAsync(s.Organizer, s.ClubTeam("T1"))).Should().Contain(p => p.GetGuid("playerId") == playerId);
    }

    [Fact]
    public async Task DRU_012_ShirtNumberTakenInTheSquad_IsRejected()
    {
        var s = await TournamentScenario.GroupsAsync(Factory, teamsPerGroup: 2, schedule: false);
        await s.CreatePlayerAsync("T1", "Jan", "Pierwszy", shirtNumber: 7);

        var second = await s.Organizer.PostAsJsonAsync($"{T(s)}/tournament-teams/{s.Team("T1")}/players/new",
            new { firstName = "Piotr", lastName = "Drugi", birthDate = new DateTime(2012, 1, 1), defaultPosition = 3, photoUrl = (string?)null, shirtNumber = 7, position = (int?)null });

        second.IsSuccessStatusCode.Should().BeFalse();
        (await SquadAsync(s, "T1")).Should().ContainSingle();
    }

    [Fact]
    public async Task DRU_014_BirthDateInTheFuture_IsRejected()
    {
        var s = await TournamentScenario.GroupsAsync(Factory, teamsPerGroup: 2, schedule: false);

        var response = await s.Organizer.PostAsJsonAsync($"{T(s)}/tournament-teams/{s.Team("T1")}/players/new",
            new { firstName = "Jan", lastName = "Jutro", birthDate = DateTime.UtcNow.Date.AddDays(1), defaultPosition = 3, photoUrl = (string?)null, shirtNumber = (int?)null, position = (int?)null });

        response.IsSuccessStatusCode.Should().BeFalse();
    }

    [Fact]
    public async Task DRU_017_DRU_028_SquadFromClubRoster_KeepsShirtNumbers_AndFlagsPlayersAlreadyInSquad()
    {
        var s = await TournamentScenario.GroupsAsync(Factory, teamsPerGroup: 2, schedule: false);
        var club = s.ClubTeam("T1");
        var ids = new List<Guid>();
        foreach (var (name, shirt) in new[] { ("Adam", 1), ("Bartek", 2), ("Cezary", 3) })
        {
            var response = await AddToRosterAsync(s.Organizer, club, name, "Kadrowy", shirt);
            response.IsSuccessStatusCode.Should().BeTrue(await response.Content.ReadAsStringAsync());
            ids.Add((await response.ReadJsonAsync()).GetGuid("playerId"));
        }

        // Źródło „kadra” podaje numery z kadry; formularz wysyła je dalej (do zmiany przed dodaniem).
        var before = (await (await s.Organizer.GetAsync($"{T(s)}/tournament-teams/{s.Team("T1")}/roster-sources")).ReadJsonAsync())
            .EnumerateArray().Single(x => x.GetString("kind") == "Roster").GetProperty("players").EnumerateArray().ToList();
        before.Select(p => p.GetInt("shirtNumber")).Should().BeEquivalentTo([1, 2, 3]);
        before.Should().OnlyContain(p => !p.GetProperty("alreadyInSquad").GetBoolean());

        var import = await s.Organizer.PostAsJsonAsync($"{T(s)}/tournament-teams/{s.Team("T1")}/players/import",
            new { sourceTournamentTeamId = (Guid?)null, players = before.Select(p => new { playerId = p.GetGuid("playerId"), shirtNumber = p.GetInt("shirtNumber"), position = (int?)null }) });
        import.IsSuccessStatusCode.Should().BeTrue(await import.Content.ReadAsStringAsync());

        var squad = await SquadAsync(s, "T1");
        squad.Select(p => p.GetInt("shirtNumber")).Should().BeEquivalentTo([1, 2, 3]);

        var sources = (await (await s.Organizer.GetAsync($"{T(s)}/tournament-teams/{s.Team("T1")}/roster-sources")).ReadJsonAsync()).EnumerateArray().ToList();
        var roster = sources.Single(x => x.GetString("kind") == "Roster");
        roster.GetProperty("players").EnumerateArray().Should().OnlyContain(p => p.GetProperty("alreadyInSquad").GetBoolean());
    }

    [Fact]
    public async Task DRU_019_CoachAndAssistant_AreAssigned_DuplicateIsRejected_AndCoachSeesTheTeam()
    {
        var s = await TournamentScenario.GroupsAsync(Factory, teamsPerGroup: 2, schedule: false);
        var (coach, coachId) = await s.JoinAsync("Coach");
        var (_, assistantId) = await s.JoinAsync("Coach");

        await s.AssignCoachAsync("T1", coachId);
        (await s.Organizer.PostAsJsonAsync($"{T(s)}/tournament-teams/{s.Team("T1")}/staff", new { userId = assistantId, role = 2 }))
            .IsSuccessStatusCode.Should().BeTrue();
        (await s.Organizer.PostAsJsonAsync($"{T(s)}/tournament-teams/{s.Team("T1")}/coaches", new { userId = coachId }))
            .IsSuccessStatusCode.Should().BeFalse();

        (await CoachTeamsAsync(coach)).Should().Contain(t => t.GetGuid("tournamentTeamId") == s.Team("T1"));
    }

    [Fact]
    public async Task DRU_021_CoachCannotChangeTheSquadAfterStart_WhenTheSettingIsOff()
    {
        var s = await TournamentScenario.GroupsAsync(Factory, teamsPerGroup: 2);
        var (coach, coachId) = await s.JoinAsync("Coach");
        await s.AssignCoachAsync("T1", coachId);

        var response = await coach.PostAsJsonAsync($"{T(s)}/tournament-teams/{s.Team("T1")}/players/new",
            new { firstName = "Późny", lastName = "Gracz", birthDate = new DateTime(2012, 1, 1), defaultPosition = 3, photoUrl = (string?)null, shirtNumber = (int?)null, position = (int?)null });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await SquadAsync(s, "T1")).Should().BeEmpty();
    }

    [Fact]
    public async Task DRU_025_DuplicateNameAndBirthDate_NeedsConfirmation()
    {
        var s = await TournamentScenario.GroupsAsync(Factory, teamsPerGroup: 2, schedule: false);
        var club = s.ClubTeam("T1");
        (await AddToRosterAsync(s.Organizer, club, "Jan", "Kowalski", 5)).IsSuccessStatusCode.Should().BeTrue();

        (await AddToRosterAsync(s.Organizer, club, "Jan", "Kowalski", 6)).IsSuccessStatusCode.Should().BeFalse();
        (await RosterAsync(s.Organizer, club)).Should().ContainSingle();

        (await AddToRosterAsync(s.Organizer, club, "Jan", "Kowalski", 6, allowDuplicate: true)).IsSuccessStatusCode.Should().BeTrue();
        (await RosterAsync(s.Organizer, club)).Should().HaveCount(2);
    }

    [Fact]
    public async Task DRU_026_ShirtNumberTakenInTheClubRoster_IsRejected()
    {
        var s = await TournamentScenario.GroupsAsync(Factory, teamsPerGroup: 2, schedule: false);
        var club = s.ClubTeam("T1");
        (await AddToRosterAsync(s.Organizer, club, "Jan", "Siódmy", 7)).IsSuccessStatusCode.Should().BeTrue();

        (await AddToRosterAsync(s.Organizer, club, "Piotr", "Inny", 7)).IsSuccessStatusCode.Should().BeFalse();
        (await RosterAsync(s.Organizer, club)).Should().ContainSingle();
    }

    [Fact]
    public async Task DRU_027b_RemovedFromRosterAndSquad_ThenRestored_ReturnsToRosterOnly_AndKeepsGoals()
    {
        var s = await TournamentScenario.GroupsAsync(Factory, teamsPerGroup: 2);
        var club = s.ClubTeam("T1");
        var (_, playerId) = await s.CreatePlayerAsync("T1", "Jan", "Odchodzący", shirtNumber: 9);
        var match = (await s.MatchBetweenAsync("T1", "T2")).GetGuid("id");
        var m = $"{T(s)}/matches/{match}";
        (await s.Organizer.PostAsync($"{m}/start", null)).IsSuccessStatusCode.Should().BeTrue();
        (await s.Organizer.PostAsJsonAsync($"{m}/goals", new { tournamentTeamId = s.Team("T1"), playerId, assistPlayerId = (Guid?)null, goalType = 5, minute = 3, second = (int?)null }))
            .IsSuccessStatusCode.Should().BeTrue();

        var remove = await s.Organizer.DeleteAsync($"/api/teams/{club}/players/{playerId}?removeFromCurrentSquads=true");
        remove.IsSuccessStatusCode.Should().BeTrue(await remove.Content.ReadAsStringAsync());
        (await SquadAsync(s, "T1")).Should().NotContain(p => p.GetGuid("playerId") == playerId && p.GetString("status") != "Removed");
        (await (await s.Organizer.GetAsync($"{m}/events")).Content.ReadAsStringAsync()).Should().Contain(playerId.ToString());

        var restore = await s.Organizer.PostAsync($"/api/teams/{club}/players/{playerId}/restore", null);
        restore.IsSuccessStatusCode.Should().BeTrue(await restore.Content.ReadAsStringAsync());
        (await RosterAsync(s.Organizer, club)).Single(p => p.GetGuid("playerId") == playerId).GetString("status").Should().Be("Active");
        (await SquadAsync(s, "T1")).Should().NotContain(p => p.GetGuid("playerId") == playerId && p.GetString("status") != "Removed");
    }

    [Fact]
    public async Task DRU_029_RemovingFromTheSquad_KeepsThePlayerInTheClubRoster()
    {
        var s = await TournamentScenario.GroupsAsync(Factory, teamsPerGroup: 2, schedule: false);
        var (coach, coachId) = await s.JoinAsync("Coach");
        await s.AssignCoachAsync("T1", coachId);
        var (tpA, playerA) = await s.CreatePlayerAsync("T1", "Adam", "Organizatora");
        var (tpB, playerB) = await s.CreatePlayerAsync("T1", "Bartek", "Trenera");
        var squadPath = $"{T(s)}/tournament-teams/{s.Team("T1")}/players";

        (await s.Organizer.DeleteAsync($"{squadPath}/{tpA}")).IsSuccessStatusCode.Should().BeTrue();
        (await coach.DeleteAsync($"{squadPath}/{tpB}")).IsSuccessStatusCode.Should().BeTrue();

        (await SquadAsync(s, "T1")).Where(p => p.GetString("status") != "Removed").Should().BeEmpty();
        var roster = await RosterAsync(s.Organizer, s.ClubTeam("T1"));
        roster.Select(p => p.GetGuid("playerId")).Should().Contain([playerA, playerB]);
    }

    [Fact]
    public async Task DRU_030_RejectedTeam_CanRegisterAgain_AsOnePendingEntry()
    {
        var s = await TournamentScenario.GroupsAsync(Factory, teamsPerGroup: 2, schedule: false);
        var (club, tt) = await RegisterTeamAsync(s, "Odrzuceni");
        (await SetStatusAsync(s, tt, Rejected)).IsSuccessStatusCode.Should().BeTrue();

        var again = await s.Organizer.PostAsJsonAsync($"{T(s)}/teams", new { teamId = club, categoryId = s.CategoryId });

        again.IsSuccessStatusCode.Should().BeTrue(await again.Content.ReadAsStringAsync());
        var entries = (await TeamsAsync(s)).Where(t => t.GetGuid("teamId") == club).ToList();
        entries.Should().ContainSingle().Which.GetString("registrationStatus").Should().Be("Pending");
    }

    [Fact]
    public async Task DRU_031_AcceptedTeam_GoesToTheOnlyGroupByItself()
    {
        var s = await TournamentScenario.GroupsAsync(Factory, teamsPerGroup: 2, schedule: false);
        var (_, tt) = await RegisterTeamAsync(s, "Jedyna grupa");
        (await EntryAsync(s, tt)).GetGuidOrNull("groupId").Should().Be(s.Group("A"), "zaraz po zgłoszeniu");

        (await SetStatusAsync(s, tt, Accepted)).IsSuccessStatusCode.Should().BeTrue();

        var entry = await EntryAsync(s, tt);
        entry.GetGuidOrNull("groupId").Should().Be(s.Group("A"), entry.GetRawText());
    }

    [Fact]
    public async Task DRU_033_RemovedCoach_LosesTheTeamInTheirPanel()
    {
        var s = await TournamentScenario.GroupsAsync(Factory, teamsPerGroup: 2, schedule: false);
        var (coach, coachId) = await s.JoinAsync("Coach");
        var staffId = await s.AssignCoachAsync("T1", coachId);

        (await s.Organizer.DeleteAsync($"{T(s)}/tournament-teams/{s.Team("T1")}/staff/{staffId}")).IsSuccessStatusCode.Should().BeTrue();

        (await CoachTeamsAsync(coach)).Should().NotContain(t => t.GetGuid("tournamentTeamId") == s.Team("T1"));
    }

    [Fact]
    public async Task DRU_034_WithdrawnTeam_DisappearsFromCoachAndParentPanels()
    {
        var s = await TournamentScenario.GroupsAsync(Factory, teamsPerGroup: 2, schedule: false);
        var (coach, coachId) = await s.JoinAsync("Coach");
        await s.AssignCoachAsync("T1", coachId);
        var (_, child) = await s.CreatePlayerAsync("T1", "Ola", "Dziecko");
        var (parent, parentId) = await s.NewUserAsync("parent");
        await s.AddGuardianAsync(child, "T1", parentId);

        (await SetStatusAsync(s, s.Team("T1"), Withdrawn)).IsSuccessStatusCode.Should().BeTrue();

        (await CoachTeamsAsync(coach)).Should().NotContain(t => t.GetGuid("tournamentTeamId") == s.Team("T1"));
        var team = await parent.GetAsync($"/api/parent/players/{child}/team");
        (await team.Content.ReadAsStringAsync()).Should().NotContain(s.Team("T1").ToString());
    }

    [Fact]
    public async Task DRU_035_CategoryWithOnlyRejectedTeams_CanBeDeleted()
    {
        var s = await TournamentScenario.GroupsAsync(Factory, teamsPerGroup: 2, schedule: false);
        var categoryId = await TournamentFlow.AddCategoryAsync(s.Organizer, s.TournamentId, "U10");
        var (_, tt) = await RegisterTeamAsync(s, "Odrzuceni U10", categoryId);
        (await SetStatusAsync(s, tt, Rejected)).IsSuccessStatusCode.Should().BeTrue();

        var delete = await s.Organizer.DeleteAsync($"{T(s)}/categories/{categoryId}");

        delete.IsSuccessStatusCode.Should().BeTrue(await delete.Content.ReadAsStringAsync());
        (await TeamsAsync(s)).Should().NotContain(t => t.GetGuid("id") == tt);
    }

    [Fact]
    public async Task DRU_036_CoachSeesTheirTeamsClubRoster()
    {
        var s = await TournamentScenario.GroupsAsync(Factory, teamsPerGroup: 2, schedule: false);
        var (coach, coachId) = await s.JoinAsync("Coach");
        await s.AssignCoachAsync("T1", coachId);
        var (_, playerId) = await s.CreatePlayerAsync("T1", "Jan", "Kadrowy");

        (await RosterAsync(coach, s.ClubTeam("T1"))).Should().Contain(p => p.GetGuid("playerId") == playerId);
    }
}

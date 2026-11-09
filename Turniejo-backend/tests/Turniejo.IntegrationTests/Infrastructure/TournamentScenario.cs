using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Turniejo.Api.Controllers;
using Turniejo.Brackets.Domain.Enums;
using Turniejo.Matches.Domain.Enums;
using Turniejo.Tournaments.Application.Tournaments.CreateTournament;
using Turniejo.Tournaments.Domain.Enums;

namespace Turniejo.IntegrationTests.Infrastructure;

/// <summary>
/// Gotowy turniej do testu jednym wywołaniem: organizacja, drużyny, grupy albo drabinka, boisko,
/// terminarz i start. Test opisuje tylko to, co sprawdza - przygotowanie danych jest tutaj.
/// Drużyny nazywają się "T1", "T2"…; <see cref="Team"/> zwraca ich zgłoszenie (tournamentTeamId).
/// </summary>
public sealed class TournamentScenario
{
    private readonly IntegrationTestWebApplicationFactory _factory;
    private readonly Dictionary<string, Guid> _teams = new();
    private readonly Dictionary<string, Guid> _groups = new();
    private readonly Dictionary<string, Guid> _clubTeams = new();

    private TournamentScenario(IntegrationTestWebApplicationFactory factory, HttpClient organizer, Guid organizationId)
    {
        _factory = factory;
        Organizer = organizer;
        OrganizationId = organizationId;
    }

    public HttpClient Organizer { get; }
    public Guid OrganizationId { get; }
    public Guid TournamentId { get; private set; }
    public Guid CategoryId { get; private set; }
    public Guid PitchId { get; private set; }
    public DateTimeOffset StartsAt { get; private set; }

    public Guid Team(string name) => _teams[name];
    public Guid Group(string name) => _groups[name];
    /// <summary>Drużyna klubu (teamId), z której powstało zgłoszenie <paramref name="name"/>.</summary>
    public Guid ClubTeam(string name) => _clubTeams[name];
    public IReadOnlyDictionary<string, Guid> Teams => _teams;

    /// <summary>Turniej grupowy: <paramref name="teamsPerGroup"/> drużyn w każdej z grup "A", "B"…</summary>
    public static async Task<TournamentScenario> GroupsAsync(
        IntegrationTestWebApplicationFactory factory, int groups = 1, int teamsPerGroup = 4,
        bool schedule = true, bool start = true, TournamentFormat format = TournamentFormat.Group)
    {
        var scenario = await CreateAsync(factory, format);
        var teamNumber = 1;
        for (var g = 0; g < groups; g++)
        {
            var groupName = ((char)('A' + g)).ToString();
            Guid groupId;
            if (g == 0)
            {
                // Nowa kategoria ma już domyślną grupę - to ona jest grupą "A", jak w aplikacji.
                var existing = (await (await scenario.Organizer.GetAsync($"/api/tournaments/{scenario.TournamentId}/groups")).ReadJsonAsync())
                    .EnumerateArray().ToList();
                groupId = existing.Single(x => x.GetGuid("categoryId") == scenario.CategoryId).GetGuid("id");
            }
            else
            {
                var groupResponse = await scenario.Organizer.PostAsJsonAsync($"/api/tournaments/{scenario.TournamentId}/groups",
                    new TournamentsController.AddTournamentGroupRequest(scenario.CategoryId, groupName, g + 1));
                groupResponse.IsSuccessStatusCode.Should().BeTrue();
                groupId = (await groupResponse.ReadJsonAsync()).GetGuid("tournamentGroupId");
            }
            scenario._groups[groupName] = groupId;

            for (var t = 0; t < teamsPerGroup; t++)
            {
                var ttId = await scenario.AddAcceptedTeamAsync($"T{teamNumber++}");
                await TournamentFlow.AssignTeamToGroupAsync(scenario.Organizer, scenario.TournamentId, ttId, groupId);
            }
        }

        await scenario.CloseRegistrationAsync();
        if (schedule)
            await scenario.GenerateScheduleAsync();
        if (schedule && start)
            await TournamentFlow.ChangeStatusAsync(scenario.Organizer, scenario.TournamentId, TournamentStatus.InProgress);
        return scenario;
    }

    /// <summary>Puchar: drużyny T1…Tn w parach T1-T2, T3-T4… (n = potęga dwójki).</summary>
    public static async Task<TournamentScenario> KnockoutAsync(
        IntegrationTestWebApplicationFactory factory, int teams = 4, bool thirdPlace = false,
        bool schedule = true, bool start = true, bool bracket = true)
    {
        var scenario = await CreateAsync(factory, TournamentFormat.Knockout);
        if (thirdPlace)
            await scenario.UpdateSettingsAsync(thirdPlace: true);

        for (var i = 1; i <= teams; i++)
            await scenario.AddAcceptedTeamAsync($"T{i}");
        await scenario.CloseRegistrationAsync();
        if (!bracket)
            return scenario;

        var pairings = Enumerable.Range(0, teams / 2)
            .Select(i => new BracketsController.BracketPairingRequest(
                BracketSourceType.Fixed, null, null, scenario.Team($"T{i * 2 + 1}"),
                BracketSourceType.Fixed, null, null, scenario.Team($"T{i * 2 + 2}")))
            .ToList();
        (await scenario.Organizer.PostAsJsonAsync($"/api/tournaments/{scenario.TournamentId}/bracket/generate",
                new BracketsController.GenerateBracketRequest(scenario.CategoryId, pairings)))
            .IsSuccessStatusCode.Should().BeTrue();

        if (schedule)
            await scenario.GenerateScheduleAsync();
        if (schedule && start)
            await TournamentFlow.ChangeStatusAsync(scenario.Organizer, scenario.TournamentId, TournamentStatus.InProgress);
        return scenario;
    }

    private static async Task<TournamentScenario> CreateAsync(IntegrationTestWebApplicationFactory factory, TournamentFormat format)
    {
        var id = Guid.NewGuid().ToString("N")[..8];
        var organizer = await TournamentFlow.RegisterAndAuthenticateAsync(factory, $"org-{id}@integration.test");
        var organizationId = await TournamentFlow.CreateOrganizationAsync(organizer, $"Org {id}");
        var scenario = new TournamentScenario(factory, organizer, organizationId);

        // Turniej zaczyna się za godzinę: mecze zaplanowane w przyszłości, przypomnienia i zegar działają jak w aplikacji.
        scenario.StartsAt = DateTimeOffset.UtcNow.AddHours(1);
        var response = await organizer.PostAsJsonAsync("/api/tournaments",
            new CreateTournamentCommand(organizationId, $"Cup {id}", scenario.StartsAt, scenario.StartsAt.AddDays(1), 32, null));
        response.IsSuccessStatusCode.Should().BeTrue();
        scenario.TournamentId = (await response.ReadJsonAsync()).GetGuid("tournamentId");

        (await organizer.PatchAsJsonAsync($"/api/tournaments/{scenario.TournamentId}/format",
                new TournamentsController.UpdateTournamentFormatRequest(format)))
            .IsSuccessStatusCode.Should().BeTrue();
        scenario.CategoryId = await TournamentFlow.AddCategoryAsync(organizer, scenario.TournamentId, "Open");
        scenario.PitchId = await TournamentFlow.AddPitchAsync(organizer, scenario.TournamentId, "Boisko 1");
        await TournamentFlow.ChangeStatusAsync(organizer, scenario.TournamentId, TournamentStatus.RegistrationOpen);
        return scenario;
    }

    public async Task<Guid> AddAcceptedTeamAsync(string name, Guid? categoryId = null)
    {
        var teamId = await TournamentFlow.CreateTeamAsync(Organizer, OrganizationId, name);
        var ttId = await TournamentFlow.AddTeamToTournamentAsync(Organizer, TournamentId, teamId, categoryId ?? CategoryId);
        await TournamentFlow.AcceptRegistrationAsync(Organizer, TournamentId, ttId);
        _teams[name] = ttId;
        _clubTeams[name] = teamId;
        return ttId;
    }

    public Task StartAsync() =>
        TournamentFlow.ChangeStatusAsync(Organizer, TournamentId, TournamentStatus.InProgress);

    public Task CloseRegistrationAsync() =>
        TournamentFlow.ChangeStatusAsync(Organizer, TournamentId, TournamentStatus.RegistrationClosed);

    public async Task GenerateScheduleAsync()
    {
        var response = await Organizer.PostAsJsonAsync($"/api/tournaments/{TournamentId}/schedule/generate",
            new MatchesController.GenerateScheduleRequest(StartsAt, StartsAt.AddHours(12), [PitchId]));
        response.IsSuccessStatusCode.Should().BeTrue(await response.Content.ReadAsStringAsync());
    }

    public async Task UpdateSettingsAsync(bool thirdPlace = false, int editWindowMinutes = 60, int matchMinutes = 30, bool coachEditsSquadAfterStart = false)
    {
        var response = await Organizer.PutAsJsonAsync($"/api/tournaments/{TournamentId}/settings",
            new TournamentsController.UpdateTournamentSettingsRequest(3, 1, 0, matchMinutes, 5, thirdPlace, editWindowMinutes, 3, 0, coachEditsSquadAfterStart));
        response.IsSuccessStatusCode.Should().BeTrue(await response.Content.ReadAsStringAsync());
    }

    /// <summary>Mecze turnieju z terminarza (JSON jak w API: id, homeTournamentTeamId, status…).</summary>
    public async Task<List<JsonElement>> ScheduleAsync() =>
        (await (await Organizer.GetAsync($"/api/tournaments/{TournamentId}/schedule")).ReadJsonAsync()).EnumerateArray().ToList();

    public async Task<JsonElement> MatchBetweenAsync(string home, string away)
    {
        var (h, a) = (Team(home), Team(away));
        return (await ScheduleAsync()).Single(m =>
            (m.GetGuid("homeTournamentTeamId") == h && m.GetGuid("awayTournamentTeamId") == a) ||
            (m.GetGuid("homeTournamentTeamId") == a && m.GetGuid("awayTournamentTeamId") == h));
    }

    /// <summary>Start, gole i koniec meczu jako organizator; wynik podany z perspektywy gospodarza w terminarzu.</summary>
    public async Task PlayAsync(Guid matchId, int homeGoals, int awayGoals, HttpClient? actor = null)
    {
        var client = actor ?? Organizer;
        var match = (await ScheduleAsync()).Single(m => m.GetGuid("id") == matchId);
        var home = match.GetGuid("homeTournamentTeamId");
        var away = match.GetGuid("awayTournamentTeamId");

        (await client.PostAsync($"/api/tournaments/{TournamentId}/matches/{matchId}/start", null)).IsSuccessStatusCode.Should().BeTrue();
        for (var i = 0; i < homeGoals; i++)
            await TournamentFlow.AddGoalAsync(client, TournamentId, matchId, home, 10 + i);
        for (var i = 0; i < awayGoals; i++)
            await TournamentFlow.AddGoalAsync(client, TournamentId, matchId, away, 40 + i);

        var winner = homeGoals == awayGoals ? (Guid?)null : homeGoals > awayGoals ? home : away;
        var finish = await client.PostAsJsonAsync($"/api/tournaments/{TournamentId}/matches/{matchId}/finish",
            new MatchesController.FinishMatchRequest(MatchResultType.Regular, winner));
        finish.IsSuccessStatusCode.Should().BeTrue(await finish.Content.ReadAsStringAsync());
        await _factory.FlushOutboxAsync();
    }

    public async Task AssignRefereeAsync(Guid matchId, Guid refereeId) =>
        (await Organizer.PatchAsJsonAsync($"/api/tournaments/{TournamentId}/matches/{matchId}/referee", new { refereeId }))
            .IsSuccessStatusCode.Should().BeTrue();

    /// <summary>Trener zgłoszenia <paramref name="team"/>; zwraca teamStaffId.</summary>
    public async Task<Guid> AssignCoachAsync(string team, Guid userId)
    {
        var response = await Organizer.PostAsJsonAsync($"/api/tournaments/{TournamentId}/tournament-teams/{Team(team)}/coaches", new { userId });
        response.IsSuccessStatusCode.Should().BeTrue(await response.Content.ReadAsStringAsync());
        return (await response.ReadJsonAsync()).GetGuid("teamStaffId");
    }

    /// <summary>Nowy zawodnik w składzie (i kadrze klubu) drużyny <paramref name="team"/>.</summary>
    public async Task<(Guid TournamentPlayerId, Guid PlayerId)> CreatePlayerAsync(
        string team, string firstName, string lastName, int? shirtNumber = null, DateTime? birthDate = null)
    {
        var response = await Organizer.PostAsJsonAsync($"/api/tournaments/{TournamentId}/tournament-teams/{Team(team)}/players/new",
            new { firstName, lastName, birthDate = birthDate ?? new DateTime(2012, 1, 1), defaultPosition = 3, photoUrl = (string?)null, shirtNumber, position = (int?)null });
        response.IsSuccessStatusCode.Should().BeTrue(await response.Content.ReadAsStringAsync());
        var json = await response.ReadJsonAsync();
        return (json.GetGuid("tournamentPlayerId"), json.GetGuid("playerId"));
    }

    public async Task AddGuardianAsync(Guid playerId, string team, Guid userId, bool canEdit = true) =>
        (await Organizer.PostAsJsonAsync($"/api/players/{playerId}/guardians",
                new { tournamentId = TournamentId, tournamentTeamId = Team(team), userId, relationshipType = 1, canEdit, canReceiveNotifications = true }))
            .IsSuccessStatusCode.Should().BeTrue();

    /// <summary>Klient zalogowany jako nowy użytkownik bez żadnej roli.</summary>
    public async Task<(HttpClient Client, Guid UserId)> NewUserAsync(string prefix = "user")
    {
        var email = $"{prefix}-{Guid.NewGuid():N}@integration.test";
        var userId = await TournamentFlow.SeedUserAsync(_factory, email);
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.UserIdHeader, userId.ToString());
        client.DefaultRequestHeaders.Add(TestAuthHandler.EmailHeader, email);
        return (client, userId);
    }

    public async Task<JsonElement> TournamentAsync() =>
        await (await Organizer.GetAsync($"/api/tournaments/{TournamentId}")).ReadJsonAsync();

    /// <summary>Nowy użytkownik dołączony kodem turnieju jako sędzia, trener albo kibic.</summary>
    public async Task<(HttpClient Client, Guid UserId)> JoinAsync(string role)
    {
        var (client, userId) = await NewUserAsync(role.ToLowerInvariant());
        await JoinWithAsync(client, role);
        return (client, userId);
    }

    /// <summary>Kod turnieju dla roli: Referee, Coach albo Viewer.</summary>
    public async Task<string> CodeAsync(string role)
    {
        var tournament = await TournamentAsync();
        return role switch
        {
            "Referee" => tournament.GetString("refereeCode"),
            "Coach" => tournament.GetString("coachCode"),
            "Viewer" => tournament.GetString("viewerCode"),
            _ => throw new ArgumentOutOfRangeException(nameof(role), role, "Referee, Coach albo Viewer"),
        };
    }

    public async Task JoinWithAsync(HttpClient client, string role) =>
        (await client.PostAsJsonAsync("/api/tournaments/join", new { code = await CodeAsync(role) })).IsSuccessStatusCode.Should().BeTrue();

    /// <summary>Powiadomienia użytkownika (po przetworzeniu outboxa).</summary>
    public async Task<List<JsonElement>> NotificationsAsync(HttpClient client)
    {
        await _factory.FlushOutboxAsync();
        return (await (await client.GetAsync("/api/notifications")).ReadJsonAsync()).EnumerateArray().ToList();
    }
}

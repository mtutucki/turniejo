using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using FluentAssertions;
using Turniejo.IntegrationTests.Infrastructure;

namespace Turniejo.IntegrationTests.Scenarios;

/// <summary>
/// Macierz uprawnień (QA v3: KON-019…037). Jeden turniej A ze wszystkimi rolami i drugi organizator
/// z własnym turniejem B. Każdy endpoint zmieniający stan jest wołany przez każdą rolę:
/// - rola spoza listy dozwolonych musi dostać 403,
/// - rola dozwolona nie może dostać 401/403 (błąd biznesowy, np. 400, jest w porządku),
/// - podmiana identyfikatorów (swój turniej albo swoja drużyna + cudzy mecz, zawodnik, grupa)
///   nie może się udać.
/// Wynik to jedna lista rozbieżności, żeby od razu widzieć całą macierz, a nie pierwszy błąd.
/// </summary>
public sealed class PermissionMatrixTests(IntegrationTestWebApplicationFactory factory) : IntegrationTestBase(factory)
{
    private const string OrgA = "organizator", OrgB = "organizator B", R1 = "sędzia meczu", R2 = "sędzia innego meczu",
        C1 = "trener", C2 = "trener innej drużyny", Parent = "rodzic", Viewer = "kibic", Anon = "anonim";

    private static readonly string[] AllActors = [OrgA, OrgB, R1, R2, C1, C2, Parent, Viewer, Anon];

    private sealed record Call(string Name, HttpMethod Method, string Url, Func<HttpContent?> Body, string[] Allowed);

    private sealed record Idor(string Name, string Actor, HttpMethod Method, string Url, Func<HttpContent?> Body);

    private sealed class World
    {
        public required Dictionary<string, HttpClient> Clients { get; init; }
        public required Dictionary<string, Guid> UserIds { get; init; }
        public required TournamentScenario A { get; init; }
        public required TournamentScenario B { get; init; }
        public Guid M1, M2, MB, Goal1, Tp1, P1, Tp3, P3, StaffC2, LabelA, LabelB;
        public Guid? NotificationOfC1;
    }

    [Fact]
    public async Task EveryRole_GetsExactlyTheAccessItShould_OnEveryMutatingEndpoint()
    {
        var w = await BuildWorldAsync();
        var problems = new List<string>();

        var calls = Calls(w);

        // 1. Odmowy. Żądanie odrzucone nie zmienia stanu, więc kolejność nie ma znaczenia.
        foreach (var call in calls)
        foreach (var actor in AllActors.Except(call.Allowed))
        {
            var status = await SendAsync(w.Clients[actor], call.Method, call.Url, call.Body());
            if (status != HttpStatusCode.Forbidden)
                problems.Add($"[odmowa] {call.Name} | {actor} | oczekiwano 403, jest {(int)status}");
        }

        // 2. Podmiana identyfikatorów: rola przechodzi sprawdzenie na swoim zasobie, a celuje w cudzy.
        foreach (var idor in Idors(w))
        {
            var status = await SendAsync(w.Clients[idor.Actor], idor.Method, idor.Url, idor.Body());
            if ((int)status is >= 200 and < 300)
                problems.Add($"[cudzy zasób] {idor.Name} | {idor.Actor} | przeszło z kodem {(int)status}");
        }

        // 3. Dozwolone role nie mogą być odbite przez autoryzację. Kolejność wywołań: od nieniszczących
        // do niszczących (usunięcie turnieju na końcu).
        foreach (var call in calls)
        foreach (var actor in call.Allowed)
        {
            var status = await SendAsync(w.Clients[actor], call.Method, call.Url, call.Body());
            if (status is HttpStatusCode.Forbidden or HttpStatusCode.Unauthorized)
                problems.Add($"[dostęp] {call.Name} | {actor} | dozwolona rola dostała {(int)status}");
        }

        problems.Should().BeEmpty("macierz uprawnień ma rozbieżności:\n" + string.Join("\n", problems));
    }

    private async Task<World> BuildWorldAsync()
    {
        // Turniej trwa, a trener zmienia kadrę po starcie tylko za zgodą organizatora.
        var a = await TournamentScenario.GroupsAsync(Factory, groups: 1, teamsPerGroup: 4, start: false);
        await a.UpdateSettingsAsync(coachEditsSquadAfterStart: true);
        await a.StartAsync();
        var b = await TournamentScenario.GroupsAsync(Factory, groups: 1, teamsPerGroup: 2);
        var org = a.Organizer;

        var m1 = (await a.MatchBetweenAsync("T1", "T2")).GetGuid("id");
        var m2 = (await a.MatchBetweenAsync("T3", "T4")).GetGuid("id");
        var mb = (await b.MatchBetweenAsync("T1", "T2")).GetGuid("id");

        var (r1, r1Id) = await a.JoinAsync("Referee");
        var (r2, r2Id) = await a.JoinAsync("Referee");
        await a.AssignRefereeAsync(m1, r1Id);
        await a.AssignRefereeAsync(m2, r2Id);

        var (c1, c1Id) = await a.JoinAsync("Coach");
        var (c2, c2Id) = await a.JoinAsync("Coach");
        await a.AssignCoachAsync("T1", c1Id);
        var staffC2 = await a.AssignCoachAsync("T3", c2Id);

        var (tp1, p1) = await a.CreatePlayerAsync("T1", "Adam", "Pierwszy");
        var (tp3, p3) = await a.CreatePlayerAsync("T3", "Celina", "Trzecia");

        var (parent, parentId) = await a.NewUserAsync("parent");
        await a.AddGuardianAsync(p1, "T1", parentId);

        var (viewer, viewerId) = await a.JoinAsync("Viewer");

        // Mecz M1 trwa i ma gola: na nim sprawdzamy edycję i cofanie zdarzeń.
        (await org.PostAsync($"/api/tournaments/{a.TournamentId}/matches/{m1}/start", null)).IsSuccessStatusCode.Should().BeTrue();
        var goal = await org.PostAsJsonAsync($"/api/tournaments/{a.TournamentId}/matches/{m1}/goals",
            new { tournamentTeamId = a.Team("T1"), playerId = (Guid?)null, assistPlayerId = (Guid?)null, goalType = 5, minute = 3, second = (int?)null });
        goal.IsSuccessStatusCode.Should().BeTrue();
        var goalId = (await goal.ReadJsonAsync()).GetGuid("matchEventId");

        // Ogłoszenie daje trenerowi powiadomienie, którego kibic nie może skasować.
        (await org.PostAsJsonAsync($"/api/tournaments/{a.TournamentId}/announcements", new { title = "Info", message = "Start o 10" }))
            .IsSuccessStatusCode.Should().BeTrue();
        var c1Notifications = await a.NotificationsAsync(c1);

        // Etykiety drużyn w bazie obu organizacji.
        var labelA = await org.PostAsJsonAsync($"/api/organizations/{a.OrganizationId}/team-labels", new { name = "Rocznik 2015" });
        labelA.IsSuccessStatusCode.Should().BeTrue();
        var labelB = await b.Organizer.PostAsJsonAsync($"/api/organizations/{b.OrganizationId}/team-labels", new { name = "Rocznik 2015" });
        labelB.IsSuccessStatusCode.Should().BeTrue();

        return new World
        {
            A = a,
            B = b,
            Clients = new()
            {
                [OrgA] = org, [OrgB] = b.Organizer, [R1] = r1, [R2] = r2, [C1] = c1, [C2] = c2,
                [Parent] = parent, [Viewer] = viewer, [Anon] = CreateClient(),
            },
            UserIds = new() { [R1] = r1Id, [R2] = r2Id, [C1] = c1Id, [C2] = c2Id, [Parent] = parentId, [Viewer] = viewerId },
            M1 = m1, M2 = m2, MB = mb, Goal1 = goalId, Tp1 = tp1, P1 = p1, Tp3 = tp3, P3 = p3, StaffC2 = staffC2,
            LabelA = (await labelA.ReadJsonAsync()).GetGuid("labelId"), LabelB = (await labelB.ReadJsonAsync()).GetGuid("labelId"),
            NotificationOfC1 = c1Notifications.Select(n => (Guid?)n.GetGuid("notificationRecipientId")).FirstOrDefault(),
        };
    }

    private static List<Call> Calls(World w)
    {
        var a = w.A;
        var t = $"/api/tournaments/{a.TournamentId}";
        var m = $"{t}/matches/{w.M1}";
        var tt1 = $"{t}/tournament-teams/{a.Team("T1")}";
        var club1 = $"/api/teams/{a.ClubTeam("T1")}/players";
        var later = a.StartsAt.AddHours(3);

        string[] organizer = [OrgA];
        string[] matchStaff = [OrgA, R1];
        string[] squad = [OrgA, C1];
        string[] child = [OrgA, C1, Parent];

        Call C(string name, HttpMethod method, string url, object? body, string[] allowed) =>
            new(name, method, url, () => body is null ? null : JsonContent.Create(body), allowed);

        return
        [
            // Turniej (KON-019, KON-025, KON-026)
            C("zmiana statusu", HttpMethod.Patch, $"{t}/status", new { tournamentStatus = 5 }, organizer),
            C("format", HttpMethod.Patch, $"{t}/format", new { tournamentFormat = 1 }, organizer),
            C("dane turnieju", HttpMethod.Put, t, new { name = "Cup", startAt = a.StartsAt, endAt = a.StartsAt.AddDays(1), teamLimit = 32, description = (string?)null }, organizer),
            C("nowa kategoria", HttpMethod.Post, $"{t}/categories", new { name = "U12", minBirthDate = (DateTime?)null, maxBirthDate = (DateTime?)null, order = 2 }, organizer),
            C("edycja kategorii", HttpMethod.Put, $"{t}/categories/{a.CategoryId}", new { name = "Open", minBirthDate = (DateTime?)null, maxBirthDate = (DateTime?)null, order = 1 }, organizer),
            C("nowa grupa", HttpMethod.Post, $"{t}/groups", new { categoryId = a.CategoryId, name = "Z", order = 9 }, organizer),
            C("edycja grupy", HttpMethod.Put, $"{t}/groups/{a.Group("A")}", new { name = "A", order = 1 }, organizer),
            C("kryteria tabeli", HttpMethod.Put, $"{t}/tiebreaker-rules", new { rules = new[] { new { order = 1, type = 3 } } }, organizer),
            C("ustawienia", HttpMethod.Put, $"{t}/settings", new { pointsForWin = 3, pointsForDraw = 1, pointsForLoss = 0, matchDurationMinutes = 30, breakDurationMinutes = 5, isThirdPlaceMatchEnabled = false, refereeEditWindowMinutes = 60, walkoverHomeScore = 3, walkoverAwayScore = 0, canCoachEditSquadAfterStart = true }, organizer),
            C("strona publiczna", HttpMethod.Put, $"{t}/public-settings", new { isPublic = true, showTeams = true, showPlayers = true, showSchedule = true, showStandings = true, showBracket = true, showLiveMatches = true, showStats = true }, organizer),
            C("ogłoszenie", HttpMethod.Post, $"{t}/announcements", new { title = "Hej", message = "Treść" }, organizer),

            // Drużyny w turnieju
            C("zgłoszenie drużyny", HttpMethod.Post, $"{t}/teams", new { teamId = a.ClubTeam("T1"), categoryId = a.CategoryId }, organizer),
            C("status zgłoszenia", HttpMethod.Patch, $"{tt1}/registration-status", new { registrationStatus = 2 }, organizer),
            C("status płatności", HttpMethod.Patch, $"{tt1}/payment-status", new { paymentStatus = 3 }, organizer),
            C("przydział do grupy", HttpMethod.Patch, $"{tt1}/group", new { groupId = a.Group("A") }, organizer),
            C("dodanie sztabu", HttpMethod.Post, $"{tt1}/staff", new { userId = w.UserIds[Viewer], role = 3 }, organizer),
            C("dodanie trenera", HttpMethod.Post, $"{tt1}/coaches", new { userId = w.UserIds[Viewer] }, organizer),

            // Boiska i terminarz
            C("nowe boisko", HttpMethod.Post, $"{t}/pitches", new { name = "Boisko 9", description = (string?)null }, organizer),
            C("edycja boiska", HttpMethod.Put, $"{t}/pitches/{a.PitchId}", new { name = "Boisko 1", description = (string?)null, isActive = true }, organizer),
            C("termin meczu", HttpMethod.Put, $"{t}/matches/{w.M2}/schedule", new { pitchId = a.PitchId, scheduledAt = later }, organizer),
            C("boisko meczu", HttpMethod.Patch, $"{t}/matches/{w.M2}/pitch", new { pitchId = a.PitchId }, organizer),
            C("godzina meczu", HttpMethod.Patch, $"{t}/matches/{w.M2}/time", new { scheduledAt = later }, organizer),
            C("sędzia meczu", HttpMethod.Patch, $"{t}/matches/{w.M2}/referee", new { refereeId = w.UserIds[R2] }, organizer),
            C("termin w drabince", HttpMethod.Put, $"{t}/bracket-nodes/{Guid.NewGuid()}/schedule", new { pitchId = a.PitchId, scheduledAt = later }, organizer),
            C("sędzia w drabince", HttpMethod.Patch, $"{t}/bracket-nodes/{Guid.NewGuid()}/referee", new { refereeId = w.UserIds[R2] }, organizer),

            // Drabinka (KON-034)
            C("generowanie drabinki", HttpMethod.Post, $"{t}/bracket/generate", new { categoryId = a.CategoryId, roundOnePairings = Array.Empty<object>() }, organizer),
            C("zamiana slotów", HttpMethod.Post, $"{t}/bracket/swap-slots", new { categoryId = a.CategoryId, firstNodeId = Guid.NewGuid(), firstSlot = 1, secondNodeId = Guid.NewGuid(), secondSlot = 2 }, organizer),
            C("przeliczenie drabinki", HttpMethod.Post, $"{t}/bracket/recalculate?categoryId={a.CategoryId}", null, organizer),
            C("podział drugiej fazy", HttpMethod.Put, $"{t}/second-stage/seats", new { categoryId = a.CategoryId, seats = Array.Empty<object>() }, organizer),

            // Mecz na żywo (KON-020, KON-027, KON-028, KON-029)
            C("pauza", HttpMethod.Post, $"{m}/pause", null, matchStaff),
            C("wznowienie", HttpMethod.Post, $"{m}/resume", null, matchStaff),
            C("gol", HttpMethod.Post, $"{m}/goals", new { tournamentTeamId = a.Team("T1"), playerId = (Guid?)null, assistPlayerId = (Guid?)null, goalType = 5, minute = 5, second = (int?)null }, matchStaff),
            C("kartka", HttpMethod.Post, $"{m}/cards", new { tournamentTeamId = a.Team("T1"), playerId = w.P1, cardType = 8, minute = 6, second = (int?)null }, matchStaff),
            C("asysta", HttpMethod.Post, $"{m}/assists", new { tournamentTeamId = a.Team("T1"), playerId = w.P1, scorerPlayerId = (Guid?)null, minute = 7, second = (int?)null }, matchStaff),
            C("MVP", HttpMethod.Post, $"{m}/mvp", new { tournamentTeamId = a.Team("T1"), playerId = w.P1 }, matchStaff),
            C("edycja zdarzenia", HttpMethod.Put, $"{m}/events/{w.Goal1}", new { type = 5, minute = 4, second = (int?)null, playerId = (Guid?)null, relatedPlayerId = (Guid?)null }, matchStaff),
            C("strzelec gola", HttpMethod.Patch, $"{m}/events/{w.Goal1}/players", new { playerId = w.P1, assistPlayerId = (Guid?)null }, matchStaff),
            C("ręczny wynik", HttpMethod.Patch, $"{m}/score", new { homeScore = 2, awayScore = 0 }, matchStaff),
            C("koniec meczu", HttpMethod.Post, $"{m}/finish", new { resultType = 2, winnerTournamentTeamId = a.Team("T1") }, matchStaff),
            C("korekta wyniku", HttpMethod.Patch, $"{m}/correct-score", new { homeScore = 2, awayScore = 0, winnerTournamentTeamId = a.Team("T1") }, matchStaff),
            C("wynik bez zegara", HttpMethod.Post, $"{t}/matches/{w.M1}/record-result", new { homeScore = 1, awayScore = 0, resultType = 2, winnerTournamentTeamId = a.Team("T1") }, matchStaff),
            C("zmiana rozstrzygnięcia", HttpMethod.Patch, $"{m}/result", new { resultType = 2, winnerTournamentTeamId = a.Team("T1") }, matchStaff),
            C("start meczu", HttpMethod.Post, $"{t}/matches/{w.M1}/start", null, matchStaff),

            // Pula sędziów (KON-030, KON-031)
            C("wzięcie meczu z puli", HttpMethod.Post, $"{t}/matches/{w.M2}/referee/claim", null, [R1, R2]),

            // Kadra w turnieju (KON-032, KON-037)
            C("nowy zawodnik w kadrze", HttpMethod.Post, $"{tt1}/players/new", new { firstName = "Nowy", lastName = "Gracz", birthDate = new DateTime(2012, 1, 1), defaultPosition = 3, photoUrl = (string?)null, shirtNumber = (int?)null, position = (int?)null }, squad),
            C("zawodnik do kadry", HttpMethod.Post, $"{tt1}/players", new { playerId = w.P1, shirtNumber = (int?)null, position = (int?)null }, squad),
            C("numer i status", HttpMethod.Patch, $"{tt1}/players/{w.Tp1}", new { shirtNumber = 7, position = 3, status = 1 }, squad),
            C("dane zawodnika w kadrze", HttpMethod.Put, $"{tt1}/players/{w.Tp1}", new { firstName = "Adam", lastName = "Pierwszy", birthDate = new DateTime(2012, 1, 1), photoUrl = (string?)null, shirtNumber = 7, position = 3, status = 1 }, squad),
            C("import kadry", HttpMethod.Post, $"{tt1}/players/import", new { sourceTournamentTeamId = (Guid?)null, players = Array.Empty<object>() }, squad),
            C("numer (panel trenera)", HttpMethod.Patch, $"/api/tournament-players/{w.Tp1}", new { tournamentId = a.TournamentId, tournamentTeamId = a.Team("T1"), shirtNumber = 8, position = 3, status = 1 }, squad),
            C("zawodnik do kadry (panel trenera)", HttpMethod.Post, $"/api/tournament-teams/{a.Team("T1")}/players", new { tournamentId = a.TournamentId, playerId = w.P1, shirtNumber = (int?)null, position = (int?)null }, squad),
            C("opiekun zawodnika", HttpMethod.Post, $"/api/players/{w.P1}/guardians", new { tournamentId = a.TournamentId, tournamentTeamId = a.Team("T1"), userId = w.UserIds[Viewer], relationshipType = 1, canEdit = false, canReceiveNotifications = true }, squad),

            // Dane zawodnika (rodzic, KON-022)
            C("dane dziecka (panel rodzica)", HttpMethod.Patch, $"/api/parent/players/{w.P1}", new { firstName = "Adam", lastName = "Pierwszy", birthDate = new DateTime(2012, 1, 1), defaultPosition = 3, photoUrl = (string?)null }, child),
            C("dane zawodnika", HttpMethod.Put, $"/api/players/{w.P1}", new { firstName = "Adam", lastName = "Pierwszy", birthDate = new DateTime(2012, 1, 1), defaultPosition = 3, photoUrl = (string?)null }, child),

            // Organizacja i kadra klubu (KON-023, KON-033, KON-035)
            C("nazwa organizacji", HttpMethod.Put, $"/api/organizations/{a.OrganizationId}", new { name = "Klub A" }, organizer),
            C("nowa drużyna", HttpMethod.Post, $"/api/organizations/{a.OrganizationId}/teams", new { name = "Nowa", shortName = (string?)null, logoUrl = (string?)null }, organizer),
            C("edycja drużyny", HttpMethod.Put, $"/api/organizations/{a.OrganizationId}/teams/{a.ClubTeam("T1")}", new { name = "T1", shortName = (string?)null }, organizer),
            C("herb drużyny", HttpMethod.Put, $"/api/organizations/{a.OrganizationId}/teams/{a.ClubTeam("T1")}/logo", new { logoUrl = (string?)null }, organizer),
            new("wgranie herbu", HttpMethod.Post, $"/api/organizations/{a.OrganizationId}/teams/logos", LogoUpload, organizer),
            C("nowa etykieta", HttpMethod.Post, $"/api/organizations/{a.OrganizationId}/team-labels", new { name = "Klub" }, organizer),
            C("nazwa etykiety", HttpMethod.Put, $"/api/organizations/{a.OrganizationId}/team-labels/{w.LabelA}", new { name = "Rocznik 2015" }, organizer),
            C("drużyny z etykietą", HttpMethod.Put, $"/api/organizations/{a.OrganizationId}/team-labels/{w.LabelA}/teams", new { teamIds = new[] { a.ClubTeam("T1") } }, organizer),
            C("etykiety drużyny", HttpMethod.Put, $"/api/organizations/{a.OrganizationId}/teams/{a.ClubTeam("T1")}/labels", new { labelIds = new[] { w.LabelA } }, organizer),
            C("zawodnik w kadrze klubu", HttpMethod.Post, club1, new { firstName = "Klub", lastName = "Owy", birthDate = new DateTime(2012, 2, 2), defaultPosition = 3, shirtNumber = (int?)null }, squad),
            C("edycja w kadrze klubu", HttpMethod.Put, $"{club1}/{w.P1}", new { firstName = "Adam", lastName = "Pierwszy", birthDate = new DateTime(2012, 1, 1), defaultPosition = 3, shirtNumber = 7 }, squad),

            // Niszczące: na końcu, żeby część "dozwolone" nie psuła sobie danych.
            C("cofnięcie zdarzenia", HttpMethod.Delete, $"{m}/events/{w.Goal1}", null, matchStaff),
            C("oddanie meczu", HttpMethod.Post, $"{t}/matches/{w.M1}/referee/release", null, [R1]),
            C("usunięcie z kadry klubu", HttpMethod.Delete, $"{club1}/{w.P1}?removeFromCurrentSquads=false", null, squad),
            C("przywrócenie do kadry klubu", HttpMethod.Post, $"{club1}/{w.P1}/restore", null, squad),
            C("usunięcie z kadry turnieju", HttpMethod.Delete, $"{tt1}/players/{w.Tp1}", null, squad),
            C("usunięcie etykiety", HttpMethod.Delete, $"/api/organizations/{a.OrganizationId}/team-labels/{w.LabelA}", null, organizer),
            C("usunięcie sztabu", HttpMethod.Delete, $"{t}/tournament-teams/{a.Team("T3")}/staff/{w.StaffC2}", null, organizer),
            C("usunięcie boiska", HttpMethod.Delete, $"{t}/pitches/{Guid.NewGuid()}", null, organizer),
            C("usunięcie grupy", HttpMethod.Delete, $"{t}/groups/{Guid.NewGuid()}", null, organizer),
            C("usunięcie kategorii", HttpMethod.Delete, $"{t}/categories/{Guid.NewGuid()}", null, organizer),
            C("reset drabinki", HttpMethod.Delete, $"{t}/bracket?categoryId={a.CategoryId}", null, organizer),
            C("generowanie terminarza", HttpMethod.Post, $"{t}/schedule/generate", new { startAt = a.StartsAt, endAt = a.StartsAt.AddHours(12), pitchIds = new[] { a.PitchId } }, organizer),
            C("czyszczenie terminarza", HttpMethod.Delete, $"{t}/schedule", null, organizer),
            C("usunięcie turnieju", HttpMethod.Delete, t, null, organizer),
        ];
    }

    private static List<Idor> Idors(World w)
    {
        var (a, b) = (w.A, w.B);
        var tb = $"/api/tournaments/{b.TournamentId}";
        var ta = $"/api/tournaments/{a.TournamentId}";
        var tt3 = $"{ta}/tournament-teams/{a.Team("T3")}";

        Idor I(string name, string actor, HttpMethod method, string url, object? body) =>
            new(name, actor, method, url, () => body is null ? null : JsonContent.Create(body));

        var list = new List<Idor>
        {
            // Organizator B: sprawdzenie roli przechodzi na jego turnieju, a cel jest w turnieju A.
            I("start cudzego meczu przez swój turniej", OrgB, HttpMethod.Post, $"{tb}/matches/{w.M1}/start", null),
            I("gol w cudzym meczu przez swój turniej", OrgB, HttpMethod.Post, $"{tb}/matches/{w.M1}/goals", new { tournamentTeamId = a.Team("T1"), playerId = (Guid?)null, assistPlayerId = (Guid?)null, goalType = 5, minute = 9, second = (int?)null }),
            I("koniec cudzego meczu przez swój turniej", OrgB, HttpMethod.Post, $"{tb}/matches/{w.M1}/finish", new { resultType = 2, winnerTournamentTeamId = a.Team("T1") }),
            I("ręczny wynik cudzego meczu przez swój turniej", OrgB, HttpMethod.Patch, $"{tb}/matches/{w.M1}/score", new { homeScore = 9, awayScore = 9 }),
            I("cofnięcie cudzego gola przez swój turniej", OrgB, HttpMethod.Delete, $"{tb}/matches/{w.M1}/events/{w.Goal1}", null),
            I("cofnięcie cudzego gola przez swój mecz", OrgB, HttpMethod.Delete, $"{tb}/matches/{w.MB}/events/{w.Goal1}", null),
            I("sędzia cudzego meczu przez swój turniej", OrgB, HttpMethod.Patch, $"{tb}/matches/{w.M1}/referee", new { refereeId = (Guid?)null }),
            I("termin cudzego meczu przez swój turniej", OrgB, HttpMethod.Put, $"{tb}/matches/{w.M1}/schedule", new { pitchId = b.PitchId, scheduledAt = b.StartsAt.AddHours(5) }),
            I("status cudzej drużyny przez swój turniej", OrgB, HttpMethod.Patch, $"{tb}/tournament-teams/{a.Team("T1")}/registration-status", new { registrationStatus = 3 }),
            I("cudza drużyna do swojej grupy", OrgB, HttpMethod.Patch, $"{tb}/tournament-teams/{a.Team("T1")}/group", new { groupId = b.Group("A") }),
            I("zawodnik dopisany do cudzej drużyny", OrgB, HttpMethod.Post, $"{tb}/tournament-teams/{a.Team("T1")}/players/new", new { firstName = "Obcy", lastName = "Gracz", birthDate = new DateTime(2012, 1, 1), defaultPosition = 3, photoUrl = (string?)null, shirtNumber = (int?)null, position = (int?)null }),
            I("usunięcie cudzego zawodnika przez swój turniej", OrgB, HttpMethod.Delete, $"{tb}/tournament-teams/{a.Team("T1")}/players/{w.Tp1}", null),
            I("usunięcie cudzego zawodnika przez swoją drużynę", OrgB, HttpMethod.Delete, $"{tb}/tournament-teams/{b.Team("T1")}/players/{w.Tp1}", null),
            I("edycja cudzej grupy przez swój turniej", OrgB, HttpMethod.Put, $"{tb}/groups/{a.Group("A")}", new { name = "X", order = 1 }),
            I("edycja cudzej kategorii przez swój turniej", OrgB, HttpMethod.Put, $"{tb}/categories/{a.CategoryId}", new { name = "X", minBirthDate = (DateTime?)null, maxBirthDate = (DateTime?)null, order = 1 }),
            I("edycja cudzego boiska przez swój turniej", OrgB, HttpMethod.Put, $"{tb}/pitches/{a.PitchId}", new { name = "X", description = (string?)null, isActive = false }),
            I("usunięcie cudzego boiska przez swój turniej", OrgB, HttpMethod.Delete, $"{tb}/pitches/{a.PitchId}", null),
            I("cudza drużyna klubu zgłoszona do swojego turnieju", OrgB, HttpMethod.Post, $"{tb}/teams", new { teamId = a.ClubTeam("T1"), categoryId = b.CategoryId }),
            I("cudzy zawodnik do swojej kadry", OrgB, HttpMethod.Post, $"{tb}/tournament-teams/{b.Team("T1")}/players", new { playerId = w.P1, shirtNumber = (int?)null, position = (int?)null }),
            I("opiekun cudzego zawodnika przez swoją drużynę", OrgB, HttpMethod.Post, $"/api/players/{w.P1}/guardians", new { tournamentId = b.TournamentId, tournamentTeamId = b.Team("T1"), userId = w.UserIds[Viewer], relationshipType = 1, canEdit = true, canReceiveNotifications = true }),

            // Organizator B: swoja organizacja w adresie, a etykieta albo drużyna z organizacji A.
            I("zmiana cudzej etykiety przez swoją organizację", OrgB, HttpMethod.Put, $"/api/organizations/{b.OrganizationId}/team-labels/{w.LabelA}", new { name = "X" }),
            I("usunięcie cudzej etykiety przez swoją organizację", OrgB, HttpMethod.Delete, $"/api/organizations/{b.OrganizationId}/team-labels/{w.LabelA}", null),
            I("cudza drużyna pod swoją etykietą", OrgB, HttpMethod.Put, $"/api/organizations/{b.OrganizationId}/team-labels/{w.LabelB}/teams", new { teamIds = new[] { a.ClubTeam("T1") } }),
            I("swoja etykieta na cudzej drużynie", OrgB, HttpMethod.Put, $"/api/organizations/{b.OrganizationId}/teams/{a.ClubTeam("T1")}/labels", new { labelIds = new[] { w.LabelB } }),
            I("cudza etykieta na swojej drużynie", OrgB, HttpMethod.Put, $"/api/organizations/{b.OrganizationId}/teams/{b.ClubTeam("T1")}/labels", new { labelIds = new[] { w.LabelA } }),

            // Sędzia M2 próbuje zmieniać zdarzenie z M1 przez swój mecz.
            I("cofnięcie gola z innego meczu przez swój mecz", R2, HttpMethod.Delete, $"{ta}/matches/{w.M2}/events/{w.Goal1}", null),
            I("edycja gola z innego meczu przez swój mecz", R2, HttpMethod.Put, $"{ta}/matches/{w.M2}/events/{w.Goal1}", new { type = 5, minute = 1, second = (int?)null, playerId = (Guid?)null, relatedPlayerId = (Guid?)null }),
            I("oddanie meczu, którego nie prowadzi", R2, HttpMethod.Post, $"{ta}/matches/{w.M1}/referee/release", null),

            // Trener T3 próbuje zmieniać zawodnika T1 przez swoją drużynę (KON-037).
            I("numer zawodnika innej drużyny", C2, HttpMethod.Patch, $"{tt3}/players/{w.Tp1}", new { shirtNumber = 99, position = 3, status = 1 }),
            I("dane zawodnika innej drużyny", C2, HttpMethod.Put, $"{tt3}/players/{w.Tp1}", new { firstName = "X", lastName = "Y", birthDate = new DateTime(2012, 1, 1), photoUrl = (string?)null, shirtNumber = 99, position = 3, status = 1 }),
            I("usunięcie zawodnika innej drużyny", C2, HttpMethod.Delete, $"{tt3}/players/{w.Tp1}", null),
            I("numer zawodnika innej drużyny (panel trenera)", C2, HttpMethod.Patch, $"/api/tournament-players/{w.Tp1}", new { tournamentId = a.TournamentId, tournamentTeamId = a.Team("T3"), shirtNumber = 99, position = 3, status = 1 }),
            I("opiekun zawodnika innej drużyny", C2, HttpMethod.Post, $"/api/players/{w.P1}/guardians", new { tournamentId = a.TournamentId, tournamentTeamId = a.Team("T3"), userId = w.UserIds[C2], relationshipType = 1, canEdit = true, canReceiveNotifications = true }),
            I("import zawodnika innej drużyny", C2, HttpMethod.Post, $"{tt3}/players/import", new { sourceTournamentTeamId = a.Team("T1"), players = new[] { new { playerId = w.P1, shirtNumber = (int?)null, position = (int?)null } } }),
            I("zawodnik innej drużyny do swojej kadry", C2, HttpMethod.Post, $"{tt3}/players", new { playerId = w.P1, shirtNumber = (int?)null, position = (int?)null }),
        };

        if (w.NotificationOfC1 is { } notification)
        {
            list.Add(I("przeczytanie cudzego powiadomienia", Viewer, HttpMethod.Patch, $"/api/notifications/{notification}/read", null));
            list.Add(I("usunięcie cudzego powiadomienia", Viewer, HttpMethod.Delete, $"/api/notifications/{notification}", null));
        }

        return list;
    }

    private static HttpContent LogoUpload()
    {
        // Najmniejszy poprawny PNG (1x1).
        var png = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==");
        var file = new ByteArrayContent(png);
        file.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        return new MultipartFormDataContent { { file, "file", "logo.png" } };
    }

    private static async Task<HttpStatusCode> SendAsync(HttpClient client, HttpMethod method, string url, HttpContent? body)
    {
        using var request = new HttpRequestMessage(method, url) { Content = body ?? (method == HttpMethod.Get || method == HttpMethod.Delete ? null : new StringContent("", Encoding.UTF8)) };
        using var response = await client.SendAsync(request);
        return response.StatusCode;
    }
}

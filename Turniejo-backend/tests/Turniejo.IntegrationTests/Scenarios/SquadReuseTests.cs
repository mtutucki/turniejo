using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Turniejo.Api.Controllers;
using Turniejo.IntegrationTests.Infrastructure;
using Turniejo.Players.Application.TournamentPlayers.ImportSquad;
using Turniejo.Players.Domain.Enums;
using Turniejo.Tournaments.Application.Tournaments.CreateTournament;

namespace Turniejo.IntegrationTests.Scenarios;

// Ta sama drużyna w dwóch turniejach: skład z pierwszego kopiowany do drugiego (te same profile,
// osobne wpisy w składzie), potem edycja zawodnika — profil wspólny, numer tylko w danym turnieju.
public sealed class SquadReuseTests(IntegrationTestWebApplicationFactory factory) : IntegrationTestBase(factory)
{
    [Fact]
    public async Task Squad_CanBeCopiedFromPreviousTournament_AndPlayerEditedAfterwards()
    {
        var organizer = await TournamentFlow.RegisterAndAuthenticateAsync(Factory, "organizer@integration.test");
        var now = DateTimeOffset.UtcNow;
        var organizationId = await TournamentFlow.CreateOrganizationAsync(organizer, "FC Integration Org");
        var teamId = await TournamentFlow.CreateTeamAsync(organizer, organizationId, "Orły");

        // Turniej 1 — bez limitu rocznika, dwóch zawodników.
        var springId = await CreateTournamentAsync(organizer, organizationId, "Wiosna", now);
        var springCategoryId = await TournamentFlow.AddCategoryAsync(organizer, springId, "Open");
        var springTeamId = await TournamentFlow.AddTeamToTournamentAsync(organizer, springId, teamId, springCategoryId);
        var springPlayers = $"/api/tournaments/{springId}/tournament-teams/{springTeamId}/players";

        var jan = await AddPlayerAsync(organizer, springPlayers, "Jan", "Kowalski", new DateTime(2012, 5, 1, 0, 0, 0, DateTimeKind.Utc), 9);
        var adam = await AddPlayerAsync(organizer, springPlayers, "Adam", "Stary", new DateTime(2010, 3, 1, 0, 0, 0, DateTimeKind.Utc), 7);

        // Turniej 2 — kategoria od rocznika 2012: Adam się nie mieści.
        var summerId = await CreateTournamentAsync(organizer, organizationId, "Lato", now.AddDays(10));
        var categoryResponse = await organizer.PostAsJsonAsync($"/api/tournaments/{summerId}/categories",
            new TournamentsController.AddTournamentCategoryRequest("U12", new DateTime(2012, 1, 1, 0, 0, 0, DateTimeKind.Utc), null, 1));
        categoryResponse.IsSuccessStatusCode.Should().BeTrue();
        var summerCategoryId = (await categoryResponse.ReadJsonAsync()).GetGuid("tournamentCategoryId");
        var summerTeamId = await TournamentFlow.AddTeamToTournamentAsync(organizer, summerId, teamId, summerCategoryId);
        var summerTeam = $"/api/tournaments/{summerId}/tournament-teams/{summerTeamId}";

        // Lista do skopiowania: jeden poprzedni skład, Adam oznaczony jako za stary.
        var sources = await (await organizer.GetAsync($"{summerTeam}/roster-sources")).ReadJsonAsync();
        var source = sources.EnumerateArray().Should().ContainSingle().Subject;
        source.GetGuid("tournamentTeamId").Should().Be(springTeamId);
        source.GetString("tournamentName").Should().Be("Wiosna");
        var sourcePlayers = source.GetProperty("players").EnumerateArray().ToList();
        sourcePlayers.Should().HaveCount(2);
        sourcePlayers.Single(p => p.GetGuid("playerId") == jan).GetProperty("ineligibility").ValueKind.Should().Be(JsonValueKind.Null);
        sourcePlayers.Single(p => p.GetGuid("playerId") == adam).GetString("ineligibility").Should().Be("TooOld");

        // Kopia całej listy odbija się od limitu rocznika i nie dodaje nikogo.
        var rejected = await organizer.PostAsJsonAsync($"{summerTeam}/players/import",
            new TournamentsController.ImportSquadRequest(springTeamId,
                [new ImportSquadPlayer(jan, 9, PlayerPosition.Forward), new ImportSquadPlayer(adam, 7, null)]));
        rejected.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await GetSquadAsync(organizer, $"{summerTeam}/players")).Should().BeEmpty();

        var imported = await organizer.PostAsJsonAsync($"{summerTeam}/players/import",
            new TournamentsController.ImportSquadRequest(springTeamId, [new ImportSquadPlayer(jan, 9, PlayerPosition.Forward)]));
        imported.IsSuccessStatusCode.Should().BeTrue();

        var summerSquad = await GetSquadAsync(organizer, $"{summerTeam}/players");
        var summerJan = summerSquad.Should().ContainSingle().Subject;
        summerJan.GetGuid("playerId").Should().Be(jan);
        summerJan.GetInt("shirtNumber").Should().Be(9);

        // Po kopii zawodnik jest już oznaczony jako obecny w składzie.
        sources = await (await organizer.GetAsync($"{summerTeam}/roster-sources")).ReadJsonAsync();
        sources.EnumerateArray().Single().GetProperty("players").EnumerateArray()
            .Single(p => p.GetGuid("playerId") == jan).GetProperty("alreadyInSquad").GetBoolean().Should().BeTrue();

        // Edycja w drugim turnieju: nazwisko idzie do wspólnego profilu, numer zostaje lokalny.
        var edited = await organizer.PutAsJsonAsync($"{summerTeam}/players/{summerJan.GetGuid("id")}",
            new TournamentsController.UpdatePlayerInTournamentTeamRequest(
                "Jan", "Nowak", new DateTime(2012, 5, 1, 0, 0, 0, DateTimeKind.Utc), null, 10, PlayerPosition.Midfielder, TournamentPlayerStatus.Injured));
        edited.IsSuccessStatusCode.Should().BeTrue();

        summerJan = (await GetSquadAsync(organizer, $"{summerTeam}/players")).Single();
        summerJan.GetString("lastName").Should().Be("Nowak");
        summerJan.GetInt("shirtNumber").Should().Be(10);
        summerJan.GetString("status").Should().Be("Injured");

        var springJan = (await GetSquadAsync(organizer, springPlayers)).Single(p => p.GetGuid("playerId") == jan);
        springJan.GetString("lastName").Should().Be("Nowak");
        springJan.GetInt("shirtNumber").Should().Be(9);
        springJan.GetString("status").Should().Be("Active");

        // Zmiana daty urodzenia poza limit kategorii jest odrzucana.
        var tooOld = await organizer.PutAsJsonAsync($"{summerTeam}/players/{summerJan.GetGuid("id")}",
            new TournamentsController.UpdatePlayerInTournamentTeamRequest(
                "Jan", "Nowak", new DateTime(2011, 5, 1, 0, 0, 0, DateTimeKind.Utc), null, 10, PlayerPosition.Midfielder, TournamentPlayerStatus.Active));
        tooOld.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ImportSquad_RejectsSourceOfAnotherTeam()
    {
        var organizer = await TournamentFlow.RegisterAndAuthenticateAsync(Factory, "organizer@integration.test");
        var now = DateTimeOffset.UtcNow;
        var organizationId = await TournamentFlow.CreateOrganizationAsync(organizer, "FC Integration Org");
        var tournamentId = await CreateTournamentAsync(organizer, organizationId, "Wiosna", now);
        var categoryId = await TournamentFlow.AddCategoryAsync(organizer, tournamentId, "Open");

        var eaglesId = await TournamentFlow.CreateTeamAsync(organizer, organizationId, "Orły");
        var hawksId = await TournamentFlow.CreateTeamAsync(organizer, organizationId, "Jastrzębie");
        var eagles = await TournamentFlow.AddTeamToTournamentAsync(organizer, tournamentId, eaglesId, categoryId);
        var hawks = await TournamentFlow.AddTeamToTournamentAsync(organizer, tournamentId, hawksId, categoryId);

        var player = await AddPlayerAsync(organizer, $"/api/tournaments/{tournamentId}/tournament-teams/{hawks}/players",
            "Jan", "Kowalski", new DateTime(2012, 5, 1, 0, 0, 0, DateTimeKind.Utc), 9);

        var response = await organizer.PostAsJsonAsync($"/api/tournaments/{tournamentId}/tournament-teams/{eagles}/players/import",
            new TournamentsController.ImportSquadRequest(hawks, [new ImportSquadPlayer(player, 9, null)]));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    private static async Task<Guid> CreateTournamentAsync(HttpClient client, Guid organizationId, string name, DateTimeOffset startAt)
    {
        var response = await client.PostAsJsonAsync("/api/tournaments",
            new CreateTournamentCommand(organizationId, name, startAt, startAt.AddDays(2), 16, null));
        response.IsSuccessStatusCode.Should().BeTrue();
        return (await response.ReadJsonAsync()).GetGuid("tournamentId");
    }

    private static async Task<Guid> AddPlayerAsync(HttpClient client, string playersPath, string firstName, string lastName, DateTime birthDate, int shirtNumber)
    {
        var response = await client.PostAsJsonAsync($"{playersPath}/new",
            new TournamentsController.CreatePlayerInTournamentTeamRequest(
                firstName, lastName, birthDate, PlayerPosition.Forward, null, shirtNumber, PlayerPosition.Forward));
        response.IsSuccessStatusCode.Should().BeTrue();
        return (await response.ReadJsonAsync()).GetGuid("playerId");
    }

    private static async Task<List<JsonElement>> GetSquadAsync(HttpClient client, string playersPath) =>
        (await (await client.GetAsync(playersPath)).ReadJsonAsync()).EnumerateArray().ToList();
}

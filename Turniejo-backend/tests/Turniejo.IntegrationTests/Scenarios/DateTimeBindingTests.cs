using System.Net;
using System.Text;
using FluentAssertions;
using Turniejo.IntegrationTests.Infrastructure;

namespace Turniejo.IntegrationTests.Scenarios;

// Kolumny dat to timestamptz — Npgsql przyjmuje tylko UTC, więc data bez strefy albo z niezerowym
// offsetem musi zostać znormalizowana przy deserializacji, a nie wywalić 500 przy SaveChanges.
public sealed class DateTimeBindingTests(IntegrationTestWebApplicationFactory factory) : IntegrationTestBase(factory)
{
    [Fact]
    public async Task CreatePlayer_AcceptsBirthDateWithoutTimeZone()
    {
        var client = await TournamentFlow.RegisterAndAuthenticateAsync(Factory, "dates1@integration.test");
        // Profil powiązany z samym sobą — inaczej PlayerAccessGuard nie pozwoli go odczytać.
        var userId = (await (await client.GetAsync("/api/me")).ReadJsonAsync()).GetGuid("userId");

        var response = await client.PostAsync("/api/players", Json($$"""
            {"firstName":"Jan","lastName":"Kowalski","birthDate":"2014-05-01","defaultPosition":0,"userId":"{{userId}}"}
            """));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var playerId = (await response.ReadJsonAsync()).GetGuid("playerId");

        var player = await (await client.GetAsync($"/api/players/{playerId}")).ReadJsonAsync();
        player.GetProperty("birthDate").GetString().Should().StartWith("2014-05-01T00:00:00");
    }

    [Fact]
    public async Task CreateTournament_AcceptsNonUtcOffset()
    {
        var client = await TournamentFlow.RegisterAndAuthenticateAsync(Factory, "dates2@integration.test");
        var organizationId = await TournamentFlow.CreateOrganizationAsync(client, "Dates Org");

        var response = await client.PostAsync("/api/tournaments", Json($$"""
            {"organizationId":"{{organizationId}}","name":"Dates Cup",
             "startAt":"2030-06-01T10:00:00+02:00","endAt":"2030-06-02T18:00:00","teamLimit":8}
            """));

        response.IsSuccessStatusCode.Should().BeTrue();
    }

    [Fact]
    public async Task CreatePlayer_Returns400_ForMalformedDate()
    {
        var client = await TournamentFlow.RegisterAndAuthenticateAsync(Factory, "dates3@integration.test");

        var response = await client.PostAsync("/api/players", Json(
            """{"firstName":"Jan","lastName":"Kowalski","birthDate":"01.05.2014","defaultPosition":0}"""));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    private static StringContent Json(string body) => new(body, Encoding.UTF8, "application/json");
}

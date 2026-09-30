using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Turniejo.Api.Controllers;
using Turniejo.IntegrationTests.Infrastructure;
using Turniejo.Tournaments.Application.Tournaments.CreateTournament;
using Turniejo.Tournaments.Domain.Enums;

namespace Turniejo.IntegrationTests.Scenarios;

// Generowanie terminarza jest jednorazowe (ScheduleGenerated) — ten scenariusz sprawdza drogę naprawy:
// wyczyszczenie terminarza wraca do zamkniętych zapisów i pozwala wygenerować go ponownie, a etapu
// ScheduleGenerated nie da się ustawić ręcznie (zostawiłoby turniej bez meczów i bez wyjścia).
public sealed class ScheduleLifecycleTests(IntegrationTestWebApplicationFactory factory) : IntegrationTestBase(factory)
{
    [Fact]
    public async Task Schedule_CanBeClearedAndRegenerated_ButStageCannotBeSetManually()
    {
        var organizer = await TournamentFlow.RegisterAndAuthenticateAsync(Factory, "lifecycle@integration.test");
        var now = DateTimeOffset.UtcNow;
        var organizationId = await TournamentFlow.CreateOrganizationAsync(organizer, "Lifecycle Org");

        var tournamentResponse = await organizer.PostAsJsonAsync("/api/tournaments",
            new CreateTournamentCommand(organizationId, "Lifecycle Cup", now, now.AddDays(1), 16, null));
        var tournamentId = (await tournamentResponse.ReadJsonAsync()).GetGuid("tournamentId");
        (await organizer.PatchAsJsonAsync($"/api/tournaments/{tournamentId}/format",
            new TournamentsController.UpdateTournamentFormatRequest(TournamentFormat.Group))).IsSuccessStatusCode.Should().BeTrue();

        var categoryId = await TournamentFlow.AddCategoryAsync(organizer, tournamentId, "Open");
        var groupId = (await (await organizer.PostAsJsonAsync($"/api/tournaments/{tournamentId}/groups",
            new TournamentsController.AddTournamentGroupRequest(categoryId, "Group A", 1))).ReadJsonAsync()).GetGuid("tournamentGroupId");

        foreach (var name in new[] { "A FC", "B FC", "C FC" })
        {
            var teamId = await TournamentFlow.CreateTeamAsync(organizer, organizationId, name);
            var tournamentTeamId = await TournamentFlow.AddTeamToTournamentAsync(organizer, tournamentId, teamId, categoryId);
            await TournamentFlow.AssignTeamToGroupAsync(organizer, tournamentId, tournamentTeamId, groupId);
            await TournamentFlow.AcceptRegistrationAsync(organizer, tournamentId, tournamentTeamId);
        }

        await TournamentFlow.ChangeStatusAsync(organizer, tournamentId, TournamentStatus.RegistrationOpen);
        await TournamentFlow.ChangeStatusAsync(organizer, tournamentId, TournamentStatus.RegistrationClosed);

        // Ręczne ScheduleGenerated jest odrzucane.
        var manual = await organizer.PatchAsJsonAsync($"/api/tournaments/{tournamentId}/status",
            new TournamentsController.ChangeTournamentStatusRequest(TournamentStatus.ScheduleGenerated));
        manual.IsSuccessStatusCode.Should().BeFalse();

        var pitchId = await TournamentFlow.AddPitchAsync(organizer, tournamentId, "Pitch 1");
        var generate = new MatchesController.GenerateScheduleRequest(now.AddMinutes(10), now.AddHours(8), [pitchId]);

        (await organizer.PostAsJsonAsync($"/api/tournaments/{tournamentId}/schedule/generate", generate)).IsSuccessStatusCode.Should().BeTrue();
        (await ScheduleCountAsync(organizer, tournamentId)).Should().Be(3);
        (await StatusAsync(organizer, tournamentId)).Should().Be("ScheduleGenerated");

        var clear = await organizer.DeleteAsync($"/api/tournaments/{tournamentId}/schedule");
        clear.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ScheduleCountAsync(organizer, tournamentId)).Should().Be(0);
        (await StatusAsync(organizer, tournamentId)).Should().Be("RegistrationClosed");

        (await organizer.PostAsJsonAsync($"/api/tournaments/{tournamentId}/schedule/generate", generate)).IsSuccessStatusCode.Should().BeTrue();
        (await ScheduleCountAsync(organizer, tournamentId)).Should().Be(3);
        (await StatusAsync(organizer, tournamentId)).Should().Be("ScheduleGenerated");
    }

    private static async Task<int> ScheduleCountAsync(HttpClient client, Guid tournamentId) =>
        (await (await client.GetAsync($"/api/tournaments/{tournamentId}/schedule")).ReadJsonAsync()).GetArrayLength();

    private static async Task<string?> StatusAsync(HttpClient client, Guid tournamentId) =>
        (await (await client.GetAsync($"/api/tournaments/{tournamentId}")).ReadJsonAsync()).GetProperty("status").GetString();
}

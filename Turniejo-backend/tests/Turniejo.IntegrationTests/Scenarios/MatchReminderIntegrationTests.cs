using System.Net.Http.Json;
using FluentAssertions;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Turniejo.Api.Controllers;
using Turniejo.IntegrationTests.Infrastructure;
using Turniejo.Notifications.Application.Notifications.CreateMatchReminderNotifications;
using Turniejo.Tournaments.Application.Tournaments.CreateTournament;
using Turniejo.Tournaments.Domain.Enums;

namespace Turniejo.IntegrationTests.Scenarios;

// Audyt: MatchReminderExistsAsync sprawdzał tylko MatchId, nie typ powiadomienia — zwykła zmiana
// terminu meczu (MatchTimeChanged, ten sam MatchId) trwale wygaszała późniejsze przypomnienie
// (MatchReminder). Ten test odtwarza dokładnie tę sekwencję na realnej bazie.
public sealed class MatchReminderIntegrationTests(IntegrationTestWebApplicationFactory factory) : IntegrationTestBase(factory)
{
    [Fact]
    public async Task MatchReminder_IsStillCreated_AfterAnUnrelatedNotificationAlreadyExistsForThatMatch()
    {
        var organizer = await TournamentFlow.RegisterAndAuthenticateAsync(Factory, "reminder-organizer@integration.test");
        var refereeUserId = await TournamentFlow.SeedUserAsync(Factory, "reminder-referee@integration.test");
        var now = DateTimeOffset.UtcNow;

        var organizationId = await TournamentFlow.CreateOrganizationAsync(organizer, "Reminder Org");

        var tournamentResponse = await organizer.PostAsJsonAsync("/api/tournaments",
            new CreateTournamentCommand(organizationId, "Reminder Cup", now, now.AddDays(2), 16, null));
        tournamentResponse.IsSuccessStatusCode.Should().BeTrue();
        var tournamentId = (await tournamentResponse.ReadJsonAsync()).GetGuid("tournamentId");

        (await organizer.PatchAsJsonAsync($"/api/tournaments/{tournamentId}/format",
                new TournamentsController.UpdateTournamentFormatRequest(TournamentFormat.Group)))
            .IsSuccessStatusCode.Should().BeTrue();

        var categoryId = await TournamentFlow.AddCategoryAsync(organizer, tournamentId, "Open");

        var groupResponse = await organizer.PostAsJsonAsync($"/api/tournaments/{tournamentId}/groups",
            new TournamentsController.AddTournamentGroupRequest(categoryId, "Group A", 1));
        groupResponse.IsSuccessStatusCode.Should().BeTrue();
        var groupId = (await groupResponse.ReadJsonAsync()).GetGuid("tournamentGroupId");

        var homeTeamId = await TournamentFlow.CreateTeamAsync(organizer, organizationId, "Home FC");
        var awayTeamId = await TournamentFlow.CreateTeamAsync(organizer, organizationId, "Away FC");
        var homeTournamentTeamId = await TournamentFlow.AddTeamToTournamentAsync(organizer, tournamentId, homeTeamId, categoryId);
        var awayTournamentTeamId = await TournamentFlow.AddTeamToTournamentAsync(organizer, tournamentId, awayTeamId, categoryId);
        await TournamentFlow.AssignTeamToGroupAsync(organizer, tournamentId, homeTournamentTeamId, groupId);
        await TournamentFlow.AssignTeamToGroupAsync(organizer, tournamentId, awayTournamentTeamId, groupId);
        await TournamentFlow.AcceptRegistrationAsync(organizer, tournamentId, homeTournamentTeamId);
        await TournamentFlow.AcceptRegistrationAsync(organizer, tournamentId, awayTournamentTeamId);
        await TournamentFlow.ChangeStatusAsync(organizer, tournamentId, TournamentStatus.RegistrationOpen);
        await TournamentFlow.ChangeStatusAsync(organizer, tournamentId, TournamentStatus.RegistrationClosed);

        var pitchId = await TournamentFlow.AddPitchAsync(organizer, tournamentId, "Pitch 1");

        var scheduleResponse = await organizer.PostAsJsonAsync($"/api/tournaments/{tournamentId}/schedule/generate",
            new MatchesController.GenerateScheduleRequest(now.AddMinutes(15), now.AddMinutes(180), [pitchId]));
        scheduleResponse.IsSuccessStatusCode.Should().BeTrue();

        var scheduleGet = await organizer.GetAsync($"/api/tournaments/{tournamentId}/schedule");
        var matchId = (await scheduleGet.ReadJsonAsync()).EnumerateArray().Single().GetGuid("id");

        // Bez odbiorcy (sędzia/staff/opiekun) CreateMatchReminderNotificationsCommandHandler pomija
        // mecz (recipients.Count == 0 -> continue) — przypisanie sędziego daje mu jednego odbiorcę.
        var refereeAssignResponse = await organizer.PatchAsJsonAsync($"/api/tournaments/{tournamentId}/matches/{matchId}/referee",
            new MatchesController.AssignRefereeToMatchRequest(refereeUserId));
        refereeAssignResponse.IsSuccessStatusCode.Should().BeTrue();

        // Zwykła zmiana terminu -> MatchTimeChanged notification z tym samym MatchId, ale innym
        // Type niż MatchReminder -- dokładnie ten scenariusz, który wcześniej trwale wygaszał przypomnienie.
        var newScheduledAt = DateTimeOffset.UtcNow.AddMinutes(10);
        var rescheduleResponse = await organizer.PatchAsJsonAsync($"/api/tournaments/{tournamentId}/matches/{matchId}/time",
            new MatchesController.RescheduleMatchRequest(newScheduledAt));
        rescheduleResponse.IsSuccessStatusCode.Should().BeTrue();

        await FlushOutboxAsync();

        using var scope = Factory.Services.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();
        var reminderResult = await sender.Send(new CreateMatchReminderNotificationsCommand(MinutesBeforeMatch: 30));

        reminderResult.IsSuccess.Should().BeTrue();
        reminderResult.Value.CreatedCount.Should().Be(1);
    }
}

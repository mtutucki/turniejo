using MediatR;
using Turniejo.Matches.Domain.Events;
using Turniejo.Notifications.Application.Abstractions;
using Turniejo.Notifications.Application.Services;
using Turniejo.Notifications.Domain.Enums;
using Turniejo.SharedKernel.Contracts;

namespace Turniejo.Notifications.Application.EventHandlers
{
    // Match.UpdateSchedule (łączna zmiana boiska+godziny naraz) podnosi tylko MatchScheduleChanged —
    // osobne AssignPitch/Reschedule podnoszą MatchPitchChanged/MatchTimeChanged, ale ta, łączna,
    // ścieżka nie miała żadnego subskrybenta w Notifications, więc nie generowała powiadomienia.
    internal sealed class MatchScheduleChangedHandler(
        NotificationPublisher publisher,
        MatchNotificationRecipientResolver recipientResolver,
        IMatchModule matchModule,
        INotificationsDbContext dbContext) : INotificationHandler<MatchScheduleChanged>
    {
        public async Task Handle(MatchScheduleChanged notification, CancellationToken cancellationToken)
        {
            var match = await matchModule.GetMatchAsync(notification.MatchId, cancellationToken);
            if (match == null)
                return;

            var recipients = await recipientResolver.ResolveAsync(
                match.RefereeId, match.HomeTournamentTeamId, match.AwayTournamentTeamId, cancellationToken);
            if (recipients.Count == 0)
                return;

            var pitchName = await NotificationPitch.NameAsync(matchModule, notification.TournamentId, notification.PitchId, cancellationToken);
            var message = pitchName is null
                ? $"Nowy termin meczu: {NotificationTime.DateAndTime(notification.ScheduledAt)}."
                : $"Nowy termin meczu: {NotificationTime.DateAndTime(notification.ScheduledAt)}, {pitchName}.";

            var publishResult = await publisher.PublishAsync(
                notification.TournamentId, NotificationType.ScheduleChanged, "Zmiana terminu meczu",
                message, createdByUserId: null, recipients, matchId: notification.MatchId, cancellationToken);

            if (publishResult.IsFailure)
                throw new InvalidOperationException($"Failed to publish MatchScheduleChanged notification: {publishResult.Error.Message}");

            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }
}

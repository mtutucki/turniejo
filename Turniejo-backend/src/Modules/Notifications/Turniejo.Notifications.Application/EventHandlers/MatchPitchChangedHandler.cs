using MediatR;
using Turniejo.Matches.Domain.Events;
using Turniejo.Notifications.Application.Abstractions;
using Turniejo.Notifications.Application.Services;
using Turniejo.Notifications.Domain.Enums;
using Turniejo.SharedKernel.Contracts;

namespace Turniejo.Notifications.Application.EventHandlers
{
    internal sealed class MatchPitchChangedHandler(
        NotificationPublisher publisher,
        MatchNotificationRecipientResolver recipientResolver,
        IMatchModule matchModule,
        INotificationsDbContext dbContext) : INotificationHandler<MatchPitchChanged>
    {
        public async Task Handle(MatchPitchChanged notification, CancellationToken cancellationToken)
        {
            var match = await matchModule.GetMatchAsync(notification.MatchId, cancellationToken);
            if (match == null)
                return;

            var recipients = await recipientResolver.ResolveAsync(
                match.RefereeId, match.HomeTournamentTeamId, match.AwayTournamentTeamId, cancellationToken);
            if (recipients.Count == 0)
                return;

            var pitchName = await NotificationPitch.NameAsync(matchModule, notification.TournamentId, notification.PitchId, cancellationToken);
            var message = pitchName is null ? "Boisko meczu zostało zmienione." : $"Mecz przeniesiony na: {pitchName}.";

            var publishResult = await publisher.PublishAsync(
                notification.TournamentId, NotificationType.PitchChanged, "Zmiana boiska",
                message, createdByUserId: null, recipients, matchId: notification.MatchId, cancellationToken);

            // Bez tego błąd publikacji był cichy — OutboxProcessor uznawał wiadomość za przetworzoną
            // mimo że nic się nie zapisało. Throw uruchamia już istniejący mechanizm retry/dead-letter.
            if (publishResult.IsFailure)
                throw new InvalidOperationException($"Failed to publish PitchChanged notification: {publishResult.Error.Message}");

            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }
}

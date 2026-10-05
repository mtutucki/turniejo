using MediatR;
using Turniejo.Matches.Domain.Events;
using Turniejo.Notifications.Application.Abstractions;
using Turniejo.Notifications.Application.Services;
using Turniejo.Notifications.Domain.Enums;
using Turniejo.SharedKernel.Contracts;

namespace Turniejo.Notifications.Application.EventHandlers
{
    internal sealed class MatchStartedHandler(
        NotificationPublisher publisher,
        MatchNotificationRecipientResolver recipientResolver,
        IMatchModule matchModule,
        INotificationsDbContext dbContext) : INotificationHandler<MatchStarted>
    {
        public async Task Handle(MatchStarted notification, CancellationToken cancellationToken)
        {
            var match = await matchModule.GetMatchAsync(notification.MatchId, cancellationToken);
            if (match == null)
                return;

            var recipients = await recipientResolver.ResolveAsync(
                match.RefereeId, match.HomeTournamentTeamId, match.AwayTournamentTeamId, cancellationToken);
            if (recipients.Count == 0)
                return;

            var publishResult = await publisher.PublishAsync(
                notification.TournamentId, NotificationType.MatchStarted, "Mecz się rozpoczął",
                "Mecz właśnie się rozpoczął.", createdByUserId: null, recipients, matchId: notification.MatchId, cancellationToken);

            // Bez tego błąd publikacji był cichy — OutboxProcessor uznawał wiadomość za przetworzoną
            // mimo że nic się nie zapisało. Throw uruchamia już istniejący mechanizm retry/dead-letter.
            if (publishResult.IsFailure)
                throw new InvalidOperationException($"Failed to publish MatchStarted notification: {publishResult.Error.Message}");

            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }
}

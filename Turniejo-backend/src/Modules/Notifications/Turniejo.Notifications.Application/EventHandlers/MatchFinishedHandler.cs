using MediatR;
using Turniejo.Matches.Domain.Events;
using Turniejo.Notifications.Application.Abstractions;
using Turniejo.Notifications.Application.Services;
using Turniejo.Notifications.Domain.Enums;
using Turniejo.SharedKernel.Contracts;

namespace Turniejo.Notifications.Application.EventHandlers
{
    internal sealed class MatchFinishedHandler(
        NotificationPublisher publisher,
        MatchNotificationRecipientResolver recipientResolver,
        IMatchModule matchModule,
        INotificationsDbContext dbContext) : INotificationHandler<MatchFinished>
    {
        public async Task Handle(MatchFinished notification, CancellationToken cancellationToken)
        {
            var match = await matchModule.GetMatchAsync(notification.MatchId, cancellationToken);
            if (match == null)
                return;

            var recipients = await recipientResolver.ResolveAsync(
                match.RefereeId, match.HomeTournamentTeamId, match.AwayTournamentTeamId, cancellationToken);
            if (recipients.Count == 0)
                return;

            var message = $"Mecz zakończony wynikiem {notification.HomeScore}:{notification.AwayScore}.";

            var publishResult = await publisher.PublishAsync(
                notification.TournamentId, NotificationType.MatchFinished, "Mecz zakończony",
                message, createdByUserId: null, recipients, matchId: notification.MatchId, cancellationToken);

            // Bez tego błąd publikacji (np. walidacja Notification.Create) był cichy — Handle kończył
            // się bez wyjątku, więc OutboxProcessor uznawał wiadomość za przetworzoną (MarkAsProcessed),
            // mimo że nic się nie zapisało. Throw uruchamia już istniejący mechanizm retry/dead-letter.
            if (publishResult.IsFailure)
                throw new InvalidOperationException($"Failed to publish MatchFinished notification: {publishResult.Error.Message}");

            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }
}

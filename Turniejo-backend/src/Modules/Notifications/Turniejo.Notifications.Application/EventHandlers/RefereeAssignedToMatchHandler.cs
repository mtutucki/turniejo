using MediatR;
using Turniejo.Matches.Domain.Events;
using Turniejo.Notifications.Application.Abstractions;
using Turniejo.Notifications.Application.Services;
using Turniejo.Notifications.Domain.Enums;

namespace Turniejo.Notifications.Application.EventHandlers
{
    // Organizator może zmienić sędziego także w trakcie meczu. Zdjęty sędzia dalej miał otwarty panel
    // i nie wiedział, dlaczego jego akcje są odrzucane - dostaje więc powiadomienie.
    internal sealed class RefereeAssignedToMatchHandler(
        NotificationPublisher publisher,
        INotificationsDbContext dbContext) : INotificationHandler<RefereeAssignedToMatch>
    {
        public async Task Handle(RefereeAssignedToMatch notification, CancellationToken cancellationToken)
        {
            // Bez poprzedniego sędziego nie ma kogo zawiadamiać; sędzia oddający mecz sam wie, że go oddał.
            if (notification.PreviousRefereeId is not { } previous ||
                previous == notification.RefereeId ||
                previous == notification.ChangedByUserId)
                return;

            var publishResult = await publisher.PublishAsync(
                notification.TournamentId, NotificationType.RefereeChanged, "Zmiana sędziego",
                "Organizator przekazał ten mecz innemu sędziemu. Nie prowadzisz go już.",
                createdByUserId: null, [previous], matchId: notification.MatchId, cancellationToken);

            if (publishResult.IsFailure)
                throw new InvalidOperationException($"Failed to publish RefereeChanged notification: {publishResult.Error.Message}");

            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }
}

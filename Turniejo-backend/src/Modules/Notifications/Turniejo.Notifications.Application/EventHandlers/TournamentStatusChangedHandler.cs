using MediatR;
using Turniejo.Notifications.Application.Abstractions;
using Turniejo.Notifications.Application.Services;
using Turniejo.Notifications.Domain.Enums;
using Turniejo.SharedKernel.Contracts;
using Turniejo.SharedKernel.Enums;
using Turniejo.Tournaments.Domain.Enums;
using Turniejo.Tournaments.Domain.Events;

namespace Turniejo.Notifications.Application.EventHandlers
{
    // Anulowanie turnieju nie generowało żadnego powiadomienia — drużyny i sędziowie dowiadywali
    // się o tym dopiero, gdy próbowali wejść na mecz. Rozsyłamy do każdego z rolą w tym turnieju,
    // tym samym mechanizmem co ręczne ogłoszenie organizatora (GetUserIdsByScopeAsync).
    internal sealed class TournamentStatusChangedHandler(
        NotificationPublisher publisher,
        IUserModule userModule,
        INotificationsDbContext dbContext) : INotificationHandler<TournamentStatusChanged>
    {
        public async Task Handle(TournamentStatusChanged notification, CancellationToken cancellationToken)
        {
            if (notification.NewStatus != TournamentStatus.Cancelled)
                return;

            var recipients = await userModule.GetUserIdsByScopeAsync(
                nameof(RoleScopeType.Tournament), notification.TournamentId, cancellationToken);
            if (recipients.Count == 0)
                return;

            var publishResult = await publisher.PublishAsync(
                notification.TournamentId, NotificationType.TournamentCancelled, "Turniej anulowany",
                "Organizator anulował ten turniej. Zaplanowane mecze zostały odwołane.",
                createdByUserId: notification.ChangedByUserId, recipients, matchId: null, cancellationToken);

            if (publishResult.IsFailure)
                throw new InvalidOperationException($"Failed to publish TournamentStatusChanged notification: {publishResult.Error.Message}");

            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }
}

using MediatR;
using Turniejo.Brackets.Domain.Events;
using Turniejo.Notifications.Application.Abstractions;
using Turniejo.Notifications.Application.Services;
using Turniejo.Notifications.Domain.Enums;
using Turniejo.SharedKernel.Contracts;
using Turniejo.SharedKernel.Enums;

namespace Turniejo.Notifications.Application.EventHandlers
{
    // QA v3: GPK-008 - korekta wyniku w grupie nie podmienia drużyny w rozpoczętym meczu pucharowym.
    // Wcześniej działo się to po cichu i organizator nie wiedział, że drabinka różni się od tabeli.
    internal sealed class BracketSlotChangeBlockedHandler(
        NotificationPublisher publisher,
        IUserModule userModule,
        INotificationsDbContext dbContext) : INotificationHandler<BracketSlotChangeBlocked>
    {
        public async Task Handle(BracketSlotChangeBlocked notification, CancellationToken cancellationToken)
        {
            var organizers = new List<Guid>();
            foreach (var userId in await userModule.GetUserIdsByScopeAsync(nameof(RoleScopeType.Tournament), notification.TournamentId, cancellationToken))
            {
                var roles = await userModule.GetUserRolesAsync(userId, cancellationToken);
                if (roles.Any(r => r.Role == nameof(UserRole.Organizer) &&
                                   r.ScopeType == nameof(RoleScopeType.Tournament) &&
                                   r.ScopeId == notification.TournamentId))
                    organizers.Add(userId);
            }

            if (organizers.Count == 0)
                return;

            var (title, body) = notification.SecondStage
                ? ("Druga faza różni się od tabeli",
                   "Po poprawce wyniku w grupie zmieniła się kolejność, ale mecz drugiej fazy już się zaczął, więc gra w nim " +
                   "dotychczasowa drużyna. Sprawdź grupy drugiej fazy i zdecyduj, co dalej.")
                : ("Drabinka różni się od tabeli",
                   "Po poprawce wyniku w grupie zmieniła się kolejność, ale mecz pucharowy już się zaczął, więc gra w nim " +
                   "dotychczasowa drużyna. Sprawdź drabinkę i zdecyduj, co dalej.");
            var publishResult = await publisher.PublishAsync(
                notification.TournamentId, NotificationType.BracketChangeBlocked, title, body,
                createdByUserId: null, organizers, matchId: notification.MatchIds.FirstOrDefault(), cancellationToken);

            if (publishResult.IsFailure)
                throw new InvalidOperationException($"Failed to publish BracketChangeBlocked notification: {publishResult.Error.Message}");

            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }
}

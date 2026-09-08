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

            var publishResult = await publisher.PublishAsync(
                notification.TournamentId, NotificationType.PitchChanged, "Zmiana boiska",
                "Boisko meczu zostało zmienione.", createdByUserId: null, recipients, matchId: notification.MatchId, cancellationToken);

            if (publishResult.IsSuccess)
                await dbContext.SaveChangesAsync(cancellationToken);
        }
    }
}

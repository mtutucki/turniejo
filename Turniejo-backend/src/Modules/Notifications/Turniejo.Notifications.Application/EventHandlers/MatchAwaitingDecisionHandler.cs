using MediatR;
using Turniejo.Matches.Domain.Events;
using Turniejo.Notifications.Application.Abstractions;
using Turniejo.Notifications.Application.Services;
using Turniejo.Notifications.Domain.Enums;
using Turniejo.SharedKernel.Contracts;
using Turniejo.SharedKernel.Enums;

namespace Turniejo.Notifications.Application.EventHandlers
{
    // Porzucony remis w pucharze: system go wstrzymał, ale tylko organizator może go rozstrzygnąć.
    internal sealed class MatchAwaitingDecisionHandler(
        NotificationPublisher publisher,
        IUserModule userModule,
        ITeamModule teamModule,
        INotificationsDbContext dbContext) : INotificationHandler<MatchAwaitingDecision>
    {
        public async Task Handle(MatchAwaitingDecision notification, CancellationToken cancellationToken)
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

            var home = await TeamNameAsync(notification.HomeTournamentTeamId, cancellationToken);
            var away = await TeamNameAsync(notification.AwayTournamentTeamId, cancellationToken);
            var message = $"Nikt nie zakończył meczu {home} - {away} ({notification.HomeScore}:{notification.AwayScore}), " +
                          "więc jest wstrzymany. Rozstrzygnij go karnymi.";

            var publishResult = await publisher.PublishAsync(
                notification.TournamentId, NotificationType.MatchAwaitingDecision, "Mecz czeka na rozstrzygnięcie",
                message, createdByUserId: null, organizers, matchId: notification.MatchId, cancellationToken);

            if (publishResult.IsFailure)
                throw new InvalidOperationException($"Failed to publish MatchAwaitingDecision notification: {publishResult.Error.Message}");

            await dbContext.SaveChangesAsync(cancellationToken);
        }

        private async Task<string> TeamNameAsync(Guid tournamentTeamId, CancellationToken cancellationToken)
        {
            var tournamentTeam = await teamModule.GetTournamentTeamAsync(tournamentTeamId, cancellationToken);
            if (tournamentTeam == null)
                return "?";

            var team = await teamModule.GetTeamAsync(tournamentTeam.TeamId, cancellationToken);
            return team?.Name ?? "?";
        }
    }
}

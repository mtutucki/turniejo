using MediatR;
using Turniejo.Brackets.Domain.Events;
using Turniejo.SharedKernel.Contracts;

namespace Turniejo.Brackets.Application.EventHandlers
{
    internal sealed class RealtimeBracketUpdatedHandler(IRealtimeService realtimeService)
        : INotificationHandler<BracketUpdated>, INotificationHandler<TeamsAdvancedToBracket>
    {
        public Task Handle(BracketUpdated notification, CancellationToken cancellationToken) =>
            realtimeService.PublishBracketUpdatedAsync(notification.TournamentId, cancellationToken);

        // Drużyny z grup (i wolne losy) trafiają do drabinki bez meczu - bez tego widok czekał na odświeżenie.
        public Task Handle(TeamsAdvancedToBracket notification, CancellationToken cancellationToken) =>
            realtimeService.PublishBracketUpdatedAsync(notification.TournamentId, cancellationToken);
    }
}

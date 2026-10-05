using MediatR;
using Turniejo.Matches.Domain.Events;
using Turniejo.SharedKernel.Contracts;

namespace Turniejo.Matches.Application.EventHandlers
{
    // Cofnięcie gola zmienia wynik (MatchScoreChanged), ale cofnięcie kartki — nie; bez tego zdarzenia
    // widzowie mieliby na osi czasu kartkę, której już nie ma.
    internal sealed class RealtimeMatchEventRevertedHandler(IRealtimeService realtimeService) : INotificationHandler<MatchEventReverted>
    {
        public Task Handle(MatchEventReverted notification, CancellationToken cancellationToken) =>
            realtimeService.PublishMatchEventRevertedAsync(notification.MatchId, notification.MatchEventId, cancellationToken);
    }
}

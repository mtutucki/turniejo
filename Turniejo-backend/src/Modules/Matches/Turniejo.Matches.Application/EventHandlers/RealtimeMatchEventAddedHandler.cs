using MediatR;
using Turniejo.Matches.Domain.Events;
using Turniejo.SharedKernel.Contracts;

namespace Turniejo.Matches.Application.EventHandlers
{
    internal sealed class RealtimeMatchEventAddedHandler(IRealtimeService realtimeService) : INotificationHandler<MatchEventAdded>
    {
        public Task Handle(MatchEventAdded notification, CancellationToken cancellationToken) =>
            realtimeService.PublishMatchEventAddedAsync(notification.MatchId, notification.MatchEventId, notification.Type, cancellationToken);
    }
}

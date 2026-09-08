using MediatR;
using Turniejo.SharedKernel.Contracts;
using Turniejo.Standings.Domain.Events;

namespace Turniejo.Standings.Application.EventHandlers
{
    internal sealed class RealtimeStandingUpdatedHandler(IRealtimeService realtimeService) : INotificationHandler<StandingUpdated>
    {
        public Task Handle(StandingUpdated notification, CancellationToken cancellationToken) =>
            realtimeService.PublishStandingUpdatedAsync(notification.TournamentId, notification.GroupId, cancellationToken);
    }
}

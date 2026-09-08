using MediatR;
using Turniejo.Matches.Domain.Events;
using Turniejo.SharedKernel.Contracts;

namespace Turniejo.Matches.Application.EventHandlers
{
    internal sealed class RealtimeMatchScoreChangedHandler(IRealtimeService realtimeService) : INotificationHandler<MatchScoreChanged>
    {
        public Task Handle(MatchScoreChanged notification, CancellationToken cancellationToken) =>
            realtimeService.PublishMatchScoreChangedAsync(notification.MatchId, notification.HomeScore, notification.AwayScore, cancellationToken);
    }
}

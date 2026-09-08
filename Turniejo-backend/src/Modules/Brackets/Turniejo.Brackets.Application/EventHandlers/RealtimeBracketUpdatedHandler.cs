using MediatR;
using Turniejo.Brackets.Domain.Events;
using Turniejo.SharedKernel.Contracts;

namespace Turniejo.Brackets.Application.EventHandlers
{
    internal sealed class RealtimeBracketUpdatedHandler(IRealtimeService realtimeService) : INotificationHandler<BracketUpdated>
    {
        public Task Handle(BracketUpdated notification, CancellationToken cancellationToken) =>
            realtimeService.PublishBracketUpdatedAsync(notification.TournamentId, cancellationToken);
    }
}

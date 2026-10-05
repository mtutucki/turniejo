using MediatR;
using Turniejo.Brackets.Application.Brackets.ResolveGroupPositionSlots;
using Turniejo.Standings.Domain.Events;

namespace Turniejo.Brackets.Application.EventHandlers
{
    internal sealed class StandingUpdatedHandler(ISender sender) : INotificationHandler<StandingUpdated>
    {
        public async Task Handle(StandingUpdated notification, CancellationToken cancellationToken)
        {
            await sender.Send(
                new ResolveGroupPositionSlotsCommand(notification.TournamentId, notification.CategoryId, notification.GroupId),
                cancellationToken);
        }
    }
}

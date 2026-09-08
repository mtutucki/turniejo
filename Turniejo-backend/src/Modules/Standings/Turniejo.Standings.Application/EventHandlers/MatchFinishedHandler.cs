using MediatR;
using Turniejo.Matches.Domain.Events;
using Turniejo.Standings.Application.Standings.RecalculateStandings;

namespace Turniejo.Standings.Application.EventHandlers
{
    internal sealed class MatchFinishedHandler(ISender sender) : INotificationHandler<MatchFinished>
    {
        public async Task Handle(MatchFinished notification, CancellationToken cancellationToken)
        {
            if (!notification.GroupId.HasValue)
                return;

            await sender.Send(new RecalculateStandingsCommand(notification.GroupId.Value), cancellationToken);
        }
    }
}

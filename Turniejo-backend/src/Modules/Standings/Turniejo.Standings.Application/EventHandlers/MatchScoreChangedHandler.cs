using MediatR;
using Turniejo.Matches.Domain.Events;
using Turniejo.Standings.Application.Standings.RecalculateStandings;

namespace Turniejo.Standings.Application.EventHandlers
{
    internal sealed class MatchScoreChangedHandler(ISender sender) : INotificationHandler<MatchScoreChanged>
    {
        public async Task Handle(MatchScoreChanged notification, CancellationToken cancellationToken)
        {
            if (!notification.GroupId.HasValue)
                return;

            await sender.Send(new RecalculateStandingsCommand(notification.GroupId.Value), cancellationToken);
        }
    }
}

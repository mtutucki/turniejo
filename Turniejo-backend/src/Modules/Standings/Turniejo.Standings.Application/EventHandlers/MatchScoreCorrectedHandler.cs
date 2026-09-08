using MediatR;
using Turniejo.Matches.Domain.Events;
using Turniejo.Standings.Application.Standings.RecalculateStandings;

namespace Turniejo.Standings.Application.EventHandlers
{
    internal sealed class MatchScoreCorrectedHandler(ISender sender) : INotificationHandler<MatchScoreCorrected>
    {
        public async Task Handle(MatchScoreCorrected notification, CancellationToken cancellationToken)
        {
            if (!notification.GroupId.HasValue)
                return;

            await sender.Send(new RecalculateStandingsCommand(notification.GroupId.Value), cancellationToken);
        }
    }
}

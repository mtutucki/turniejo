using MediatR;
using Turniejo.Brackets.Application.Brackets.AdvanceBracketWinner;
using Turniejo.Matches.Domain.Events;

namespace Turniejo.Brackets.Application.EventHandlers
{
    internal sealed class MatchFinishedHandler(ISender sender) : INotificationHandler<MatchFinished>
    {
        public async Task Handle(MatchFinished notification, CancellationToken cancellationToken)
        {
            await sender.Send(new AdvanceBracketWinnerCommand(notification.MatchId, notification.WinnerTournamentTeamId), cancellationToken);
        }
    }
}

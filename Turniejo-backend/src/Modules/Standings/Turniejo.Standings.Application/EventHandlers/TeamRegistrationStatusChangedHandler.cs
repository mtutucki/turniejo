using MediatR;
using Turniejo.SharedKernel.Contracts;
using Turniejo.Standings.Application.Abstractions;
using Turniejo.Standings.Application.Standings.RecalculateStandings;
using Turniejo.Teams.Domain.Enums;
using Turniejo.Teams.Domain.Events;

namespace Turniejo.Standings.Application.EventHandlers
{
    // Wycofanie drużyny (Accepted -> Withdrawn) zostawiało jej Standing w tabeli na zawsze —
    // RecalculateStandingsCommandHandler filtruje NOWE wiersze po RegistrationStatus=="Accepted",
    // ale nigdy nie usuwał JUŻ ISTNIEJĄCEGO wiersza drużyny, która przestała być zaakceptowana.
    internal sealed class TeamRegistrationStatusChangedHandler(
        IStandingsDbContext dbContext,
        ITeamModule teamModule,
        ISender sender) : INotificationHandler<TeamRegistrationStatusChanged>
    {
        public async Task Handle(TeamRegistrationStatusChanged notification, CancellationToken cancellationToken)
        {
            if (notification.NewStatus != RegistrationStatus.Withdrawn)
                return;

            var tournamentTeam = await teamModule.GetTournamentTeamAsync(notification.TournamentTeamId, cancellationToken);
            if (tournamentTeam?.GroupId is not { } groupId)
                return;

            var standing = await dbContext.GetStandingAsync(groupId, notification.TournamentTeamId, cancellationToken);
            if (standing != null)
            {
                await dbContext.RemoveStandingAsync(standing, cancellationToken);
                await dbContext.SaveChangesAsync(cancellationToken);
            }

            // Pozycje pozostałych drużyn w grupie muszą się przesunąć bez wycofanej.
            await sender.Send(new RecalculateStandingsCommand(groupId), cancellationToken);
        }
    }
}

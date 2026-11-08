using MediatR;
using Turniejo.SharedKernel.Contracts;
using Turniejo.Standings.Application.Standings.RecalculateStandings;
using Turniejo.Tournaments.Domain.Events;

namespace Turniejo.Standings.Application.EventHandlers
{
    // Kolejność w tabeli zależy od kryteriów rozstrzygania. Bez przeliczenia po ich zmianie tabela
    // zostawała ułożona według starych reguł aż do następnego wyniku w grupie.
    internal sealed class TieBreakerRulesSetHandler(ITournamentModule tournamentModule, ISender sender)
        : INotificationHandler<TieBreakerRulesSet>
    {
        public async Task Handle(TieBreakerRulesSet notification, CancellationToken cancellationToken)
        {
            var groups = await tournamentModule.GetTournamentGroupsAsync(notification.TournamentId, cancellationToken);
            foreach (var group in groups)
                await sender.Send(new RecalculateStandingsCommand(group.Id), cancellationToken);
        }
    }
}

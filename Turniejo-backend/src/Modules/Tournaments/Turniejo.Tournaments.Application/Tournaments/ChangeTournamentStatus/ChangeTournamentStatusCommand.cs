using Turniejo.SharedKernel.CQRS;
using Turniejo.Tournaments.Domain.Enums;

namespace Turniejo.Tournaments.Application.Tournaments.ChangeTournamentStatus
{
    public sealed record ChangeTournamentStatusCommand(Guid TournamentId, TournamentStatus TournamentStatus) : ICommand<ChangeTournamentStatusResponse>;
}

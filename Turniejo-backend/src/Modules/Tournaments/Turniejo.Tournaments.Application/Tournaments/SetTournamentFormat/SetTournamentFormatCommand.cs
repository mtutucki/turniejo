using Turniejo.SharedKernel.CQRS;
using Turniejo.Tournaments.Domain.Enums;

namespace Turniejo.Tournaments.Application.Tournaments.SetTournamentFormat
{
    public sealed record SetTournamentFormatCommand(Guid TournamentId, TournamentFormat TournamentFormat) : ICommand<SetTournamentFormatResponse>;
}

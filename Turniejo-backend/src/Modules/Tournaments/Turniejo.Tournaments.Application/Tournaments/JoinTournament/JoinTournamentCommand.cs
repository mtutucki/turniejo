using Turniejo.SharedKernel.CQRS;

namespace Turniejo.Tournaments.Application.Tournaments.JoinTournament
{
    public sealed record JoinTournamentCommand(string Code) : ICommand<JoinTournamentResponse>;
}

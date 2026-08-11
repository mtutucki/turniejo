using Turniejo.SharedKernel.CQRS;
using Turniejo.Tournaments.Application.DTOs;

namespace Turniejo.Tournaments.Application.Tournaments.GetTournamentById
{
    public sealed record GetTournamentByIdQuery(Guid TournamentId) : IQuery<TournamentDto>;
}

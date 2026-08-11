using Turniejo.SharedKernel.CQRS;
using Turniejo.Tournaments.Application.DTOs;

namespace Turniejo.Tournaments.Application.Tournaments.GetTournaments
{
    public sealed record GetTournamentsQuery(Guid OrganizationId)
        : IQuery<IReadOnlyList<TournamentDto>>;
}

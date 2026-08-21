using Turniejo.SharedKernel.CQRS;
using Turniejo.Tournaments.Application.DTOs;

namespace Turniejo.Tournaments.Application.TournamentGroups.GetTournamentGroups
{
    public sealed record GetTournamentGroupsQuery(Guid TournamentId) : IQuery<IReadOnlyList<TournamentGroupDto>>;
}

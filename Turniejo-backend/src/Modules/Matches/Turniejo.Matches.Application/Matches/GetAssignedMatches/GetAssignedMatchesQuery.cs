using Turniejo.Matches.Application.DTOs;
using Turniejo.SharedKernel.CQRS;

namespace Turniejo.Matches.Application.Matches.GetAssignedMatches
{
    public sealed record GetAssignedMatchesQuery : IQuery<IReadOnlyList<MatchDto>>;
}

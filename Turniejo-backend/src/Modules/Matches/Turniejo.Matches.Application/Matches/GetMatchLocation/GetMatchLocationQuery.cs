using Turniejo.Matches.Application.DTOs;
using Turniejo.SharedKernel.CQRS;

namespace Turniejo.Matches.Application.Matches.GetMatchLocation
{
    public sealed record GetMatchLocationQuery(Guid MatchId) : IQuery<MatchLocationDto>;
}

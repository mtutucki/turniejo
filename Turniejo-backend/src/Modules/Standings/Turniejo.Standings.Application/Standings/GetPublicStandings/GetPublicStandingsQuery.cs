using Turniejo.SharedKernel.CQRS;
using Turniejo.Standings.Application.DTOs;

namespace Turniejo.Standings.Application.Standings.GetPublicStandings
{
    public sealed record GetPublicStandingsQuery(string Slug, Guid GroupId) : IQuery<IReadOnlyList<StandingDto>>, IPublicRequest;
}

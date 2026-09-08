using Turniejo.Matches.Application.DTOs;
using Turniejo.SharedKernel.CQRS;

namespace Turniejo.Matches.Application.Schedule.GetPublicResults
{
    public sealed record GetPublicResultsQuery(string Slug) : IQuery<IReadOnlyList<MatchDto>>, IPublicRequest;
}

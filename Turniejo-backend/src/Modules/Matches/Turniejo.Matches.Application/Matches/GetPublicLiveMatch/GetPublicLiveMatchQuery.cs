using Turniejo.Matches.Application.DTOs;
using Turniejo.SharedKernel.CQRS;

namespace Turniejo.Matches.Application.Matches.GetPublicLiveMatch
{
    public sealed record GetPublicLiveMatchQuery(Guid MatchId) : IQuery<MatchLivePanelDto>, IPublicRequest;
}

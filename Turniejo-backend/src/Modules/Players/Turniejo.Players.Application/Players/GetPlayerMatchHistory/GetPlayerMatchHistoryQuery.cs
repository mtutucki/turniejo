using Turniejo.Players.Application.DTOs;
using Turniejo.SharedKernel.CQRS;

namespace Turniejo.Players.Application.Players.GetPlayerMatchHistory
{
    // Handler weryfikuje PlayerAccessGuard.CanViewAsync — ogólny odpowiednik GetMyChildResultsQuery,
    // ale dla każdego uprawnionego widza (self/guardian/Organizer/Coach), nie tylko opiekuna.
    public sealed record GetPlayerMatchHistoryQuery(Guid PlayerId) : IQuery<IReadOnlyList<MyChildMatchDto>>, IAuthenticatedOnlyRequest;
}

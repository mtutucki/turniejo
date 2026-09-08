using Turniejo.Players.Application.DTOs;
using Turniejo.SharedKernel.CQRS;

namespace Turniejo.Players.Application.PlayerGuardians.GetMyChildResults
{
    public sealed record GetMyChildResultsQuery(Guid PlayerId) : IQuery<IReadOnlyList<MyChildMatchDto>>;
}

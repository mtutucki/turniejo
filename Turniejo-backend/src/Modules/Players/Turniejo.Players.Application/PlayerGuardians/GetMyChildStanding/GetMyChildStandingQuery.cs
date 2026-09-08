using Turniejo.Players.Application.DTOs;
using Turniejo.SharedKernel.CQRS;

namespace Turniejo.Players.Application.PlayerGuardians.GetMyChildStanding
{
    public sealed record GetMyChildStandingQuery(Guid PlayerId) : IQuery<IReadOnlyList<MyChildStandingDto>>;
}

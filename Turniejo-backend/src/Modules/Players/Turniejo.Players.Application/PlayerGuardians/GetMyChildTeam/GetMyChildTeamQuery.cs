using Turniejo.Players.Application.DTOs;
using Turniejo.SharedKernel.CQRS;

namespace Turniejo.Players.Application.PlayerGuardians.GetMyChildTeam
{
    public sealed record GetMyChildTeamQuery(Guid PlayerId) : IQuery<IReadOnlyList<MyChildTeamDto>>;
}

using Turniejo.Players.Application.DTOs;
using Turniejo.SharedKernel.CQRS;

namespace Turniejo.Players.Application.PlayerGuardians.GetMyChildSchedule
{
    public sealed record GetMyChildScheduleQuery(Guid PlayerId) : IQuery<IReadOnlyList<MyChildMatchDto>>;
}

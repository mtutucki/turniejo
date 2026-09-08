using Turniejo.Players.Application.DTOs;
using Turniejo.SharedKernel.CQRS;

namespace Turniejo.Players.Application.PlayerGuardians.GetMyChildren
{
    public sealed record GetMyChildrenQuery : IQuery<IReadOnlyList<MyChildDto>>;
}

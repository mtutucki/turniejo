using Turniejo.Players.Application.DTOs;
using Turniejo.SharedKernel.CQRS;

namespace Turniejo.Players.Application.PlayerGuardians.GetMyChildren
{
    // Handler filtruje po PlayerGuardian.UserId == CurrentUserId — brak parametrów wejściowych.
    public sealed record GetMyChildrenQuery : IQuery<IReadOnlyList<MyChildDto>>, IAuthenticatedOnlyRequest;
}

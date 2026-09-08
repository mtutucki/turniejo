using Turniejo.Players.Application.DTOs;
using Turniejo.SharedKernel.CQRS;

namespace Turniejo.Players.Application.Players.GetPlayerById
{
    // Handler weryfikuje PlayerAccessGuard.CanViewAsync (self/guardian/Organizer/Coach).
    public sealed record GetPlayerByIdQuery(Guid PlayerId) : IQuery<PlayerDto>, IAuthenticatedOnlyRequest;
}

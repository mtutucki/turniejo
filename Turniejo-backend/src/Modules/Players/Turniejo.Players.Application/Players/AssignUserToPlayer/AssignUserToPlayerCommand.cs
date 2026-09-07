using Turniejo.SharedKernel.CQRS;

namespace Turniejo.Players.Application.Players.AssignUserToPlayer
{
    public sealed record AssignUserToPlayerCommand(Guid PlayerId, Guid UserId) : ICommand<AssignUserToPlayerResponse>;
}

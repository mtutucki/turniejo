using Turniejo.SharedKernel.CQRS;

namespace Turniejo.Players.Application.Players.AssignUserToPlayer
{
    // Handler weryfikuje PlayerAccessGuard.CanEditAsync (self/guardian z CanEdit/Organizer/Coach).
    public sealed record AssignUserToPlayerCommand(Guid PlayerId, Guid UserId) : ICommand<AssignUserToPlayerResponse>, IAuthenticatedOnlyRequest;
}

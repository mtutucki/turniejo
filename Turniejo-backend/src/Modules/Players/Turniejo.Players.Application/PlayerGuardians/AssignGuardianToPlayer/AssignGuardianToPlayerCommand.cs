using Turniejo.Players.Domain.Enums;
using Turniejo.SharedKernel.CQRS;

namespace Turniejo.Players.Application.PlayerGuardians.AssignGuardianToPlayer
{
    public sealed record AssignGuardianToPlayerCommand(
        Guid PlayerId,
        Guid UserId,
        RelationshipType RelationshipType,
        bool CanEdit,
        bool CanReceiveNotifications) : ICommand<AssignGuardianToPlayerResponse>;
}

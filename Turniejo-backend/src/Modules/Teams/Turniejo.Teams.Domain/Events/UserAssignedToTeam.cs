using Turniejo.SharedKernel.Primitives;
using Turniejo.Teams.Domain.Enums;

namespace Turniejo.Teams.Domain.Events
{
    public sealed record UserAssignedToTeam(
        Guid TeamStaffId,
        Guid TournamentTeamId,
        Guid UserId,
        TeamStaffRole Role,
        Guid AssignedByUserId) : IOutboxEvent
    {
        public Guid EventId { get; } = Guid.NewGuid();
        public DateTimeOffset OccurredAt { get; } = DateTimeOffset.UtcNow;
    }
}

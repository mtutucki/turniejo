using Turniejo.SharedKernel.Primitives;

namespace Turniejo.Teams.Domain.Events
{
    public sealed record CoachAssignedToTeam(
        Guid TeamStaffId,
        Guid TournamentTeamId,
        Guid UserId,
        Guid AssignedByUserId) : IOutboxEvent
    {
        public Guid EventId { get; } = Guid.NewGuid();
        public DateTimeOffset OccurredAt { get; } = DateTimeOffset.UtcNow;
    }
}

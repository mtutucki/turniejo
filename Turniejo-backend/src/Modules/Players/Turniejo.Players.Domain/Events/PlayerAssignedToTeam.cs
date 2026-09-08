using Turniejo.SharedKernel.Primitives;

namespace Turniejo.Players.Domain.Events
{
    public sealed record PlayerAssignedToTeam(
        Guid TournamentPlayerId,
        Guid TournamentTeamId,
        Guid PlayerId,
        Guid AssignedByUserId) : IOutboxEvent
    {
        public Guid EventId { get; } = Guid.NewGuid();
        public DateTimeOffset OccurredAt { get; } = DateTimeOffset.UtcNow;
    }
}

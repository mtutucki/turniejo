using Turniejo.SharedKernel.Primitives;

namespace Turniejo.Players.Domain.Events
{
    public sealed record TournamentPlayerUpdated(
        Guid TournamentPlayerId,
        Guid TournamentTeamId) : IOutboxEvent
    {
        public Guid EventId { get; } = Guid.NewGuid();
        public DateTimeOffset OccurredAt { get; } = DateTimeOffset.UtcNow;
    }
}

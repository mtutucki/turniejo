using Turniejo.SharedKernel.Primitives;

namespace Turniejo.Matches.Domain.Events
{
    public sealed record RefereeAssignedToMatch(
        Guid MatchId,
        Guid TournamentId,
        Guid? RefereeId,
        Guid ChangedByUserId) : IOutboxEvent
    {
        public Guid EventId { get; } = Guid.NewGuid();
        public DateTimeOffset OccurredAt { get; } = DateTimeOffset.UtcNow;
    }
}

using Turniejo.SharedKernel.Primitives;

namespace Turniejo.Matches.Domain.Events
{
    public sealed record RefereeAssignedToMatch(
        Guid MatchId,
        Guid TournamentId,
        Guid? RefereeId,
        Guid ChangedByUserId,
        // Sędzia przed zmianą - dostaje powiadomienie, że nie prowadzi już meczu. Null w starych wpisach outboxa.
        Guid? PreviousRefereeId = null) : IOutboxEvent
    {
        public Guid EventId { get; } = Guid.NewGuid();
        public DateTimeOffset OccurredAt { get; } = DateTimeOffset.UtcNow;
    }
}

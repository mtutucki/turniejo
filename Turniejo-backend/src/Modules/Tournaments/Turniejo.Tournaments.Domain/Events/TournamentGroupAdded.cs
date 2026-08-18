using Turniejo.SharedKernel.Primitives;

namespace Turniejo.Tournaments.Domain.Events
{
    public sealed record TournamentGroupAdded(
       Guid TournamentId,
       Guid GroupId,
       Guid OrganizationId,
       Guid AddedByUserId,
       string GroupName) : IOutboxEvent
    {
        public Guid EventId { get; } = Guid.NewGuid();
        public DateTimeOffset OccurredAt { get; } = DateTimeOffset.UtcNow;
    }
}

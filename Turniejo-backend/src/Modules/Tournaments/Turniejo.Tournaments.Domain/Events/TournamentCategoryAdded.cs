using Turniejo.SharedKernel.Primitives;

namespace Turniejo.Tournaments.Domain.Events
{
    public sealed record TournamentCategoryAdded(
        Guid TournamentId,
        Guid CategoryId,
        Guid OrganizationId,
        Guid AddedByUserId,
        string CategoryName) : IOutboxEvent
    {
        public Guid EventId { get; } = Guid.NewGuid();
        public DateTimeOffset OccurredAt { get; } = DateTimeOffset.UtcNow;
    }
}
using Turniejo.SharedKernel.Primitives;

namespace Turniejo.Teams.Domain.Events
{
    public sealed record TeamAddedToTournament(
        Guid TournamentTeamId,
        Guid TournamentId,
        Guid TeamId,
        Guid CategoryId,
        Guid AddedByUserId) : IOutboxEvent
    {
        public Guid EventId { get; } = Guid.NewGuid();
        public DateTimeOffset OccurredAt { get; } = DateTimeOffset.UtcNow;
    }
}

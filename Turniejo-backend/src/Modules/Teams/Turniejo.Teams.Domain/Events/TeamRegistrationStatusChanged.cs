using Turniejo.SharedKernel.Primitives;
using Turniejo.Teams.Domain.Enums;

namespace Turniejo.Teams.Domain.Events
{
    public sealed record TeamRegistrationStatusChanged(
        Guid TournamentTeamId,
        Guid TournamentId,
        RegistrationStatus OldStatus,
        RegistrationStatus NewStatus,
        Guid ChangedByUserId) : IOutboxEvent
    {
        public Guid EventId { get; } = Guid.NewGuid();
        public DateTimeOffset OccurredAt { get; } = DateTimeOffset.UtcNow;
    }
}

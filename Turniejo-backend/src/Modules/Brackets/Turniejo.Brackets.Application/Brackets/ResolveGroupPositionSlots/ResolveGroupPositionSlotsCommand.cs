using Turniejo.SharedKernel.CQRS;

namespace Turniejo.Brackets.Application.Brackets.ResolveGroupPositionSlots
{
    // Internal — wołane wyłącznie przez StandingUpdatedHandler (Outbox), nigdy z kontrolera.
    internal sealed record ResolveGroupPositionSlotsCommand(
        Guid TournamentId, Guid CategoryId, Guid GroupId) : ICommand<ResolveGroupPositionSlotsResponse>, IPublicRequest;
}

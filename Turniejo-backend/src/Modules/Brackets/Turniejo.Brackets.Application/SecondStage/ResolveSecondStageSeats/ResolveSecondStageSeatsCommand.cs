using Turniejo.SharedKernel.CQRS;

namespace Turniejo.Brackets.Application.SecondStage.ResolveSecondStageSeats
{
    // Internal - wołane wyłącznie przez StandingUpdatedHandler (Outbox), nigdy z kontrolera.
    internal sealed record ResolveSecondStageSeatsCommand(Guid TournamentId, Guid GroupId) : ICommand<bool>, IPublicRequest;
}

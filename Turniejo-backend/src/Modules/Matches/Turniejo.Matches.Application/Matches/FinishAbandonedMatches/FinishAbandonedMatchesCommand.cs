using Turniejo.SharedKernel.CQRS;

namespace Turniejo.Matches.Application.Matches.FinishAbandonedMatches
{
    // Wołane tylko przez AbandonedMatchBackgroundService, nigdy przez HTTP: IPublicRequest pomija
    // AuthorizationBehavior. Pilnuje tego PublicRequestSafetyTests.
    public sealed record FinishAbandonedMatchesCommand(DateTimeOffset Now)
        : ICommand<FinishAbandonedMatchesResponse>, IPublicRequest;

    public sealed record FinishAbandonedMatchesResponse(int FinishedCount, int AwaitingDecisionCount);
}

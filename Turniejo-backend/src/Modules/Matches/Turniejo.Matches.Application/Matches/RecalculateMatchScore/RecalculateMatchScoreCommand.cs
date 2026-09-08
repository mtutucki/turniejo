using Turniejo.SharedKernel.CQRS;

namespace Turniejo.Matches.Application.Matches.RecalculateMatchScore
{
    // Internal — wołane wyłącznie przez AddGoal/AddCard/AddAssist/RevertMatchEvent itd. z ISender,
    // nigdy z kontrolera. Autoryzacja już przeszła w komendzie wywołującej.
    internal sealed record RecalculateMatchScoreCommand(Guid MatchId) : ICommand<RecalculateMatchScoreResponse>, IPublicRequest;
}

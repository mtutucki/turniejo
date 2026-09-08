using Turniejo.SharedKernel.CQRS;

namespace Turniejo.Matches.Application.Matches.RecalculateMatchScore
{
    internal sealed record RecalculateMatchScoreCommand(Guid MatchId) : ICommand<RecalculateMatchScoreResponse>;
}

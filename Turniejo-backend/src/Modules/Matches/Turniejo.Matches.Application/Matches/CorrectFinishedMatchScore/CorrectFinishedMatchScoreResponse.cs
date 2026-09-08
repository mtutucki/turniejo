namespace Turniejo.Matches.Application.Matches.CorrectFinishedMatchScore
{
    public sealed record CorrectFinishedMatchScoreResponse(Guid MatchId, int HomeScore, int AwayScore);
}

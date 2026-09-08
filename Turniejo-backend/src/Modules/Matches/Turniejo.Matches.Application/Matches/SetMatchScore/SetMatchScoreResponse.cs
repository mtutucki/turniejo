namespace Turniejo.Matches.Application.Matches.SetMatchScore
{
    public sealed record SetMatchScoreResponse(Guid MatchId, int HomeScore, int AwayScore);
}

namespace Turniejo.Matches.Application.Matches.RecordMatchResult
{
    public sealed record RecordMatchResultResponse(Guid MatchId, int HomeScore, int AwayScore);
}

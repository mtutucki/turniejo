namespace Turniejo.Matches.Application.DTOs
{
    public sealed record MatchScoreDto(Guid MatchId, int HomeScore, int AwayScore);
}

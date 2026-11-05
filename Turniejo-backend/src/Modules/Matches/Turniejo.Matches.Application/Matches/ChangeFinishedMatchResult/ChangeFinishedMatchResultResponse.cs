namespace Turniejo.Matches.Application.Matches.ChangeFinishedMatchResult
{
    public sealed record ChangeFinishedMatchResultResponse(Guid MatchId, int HomeScore, int AwayScore, Guid? WinnerTournamentTeamId);
}

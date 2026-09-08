namespace Turniejo.Matches.Application.Matches.UpdateMatchEvent
{
    public sealed record UpdateMatchEventResponse(Guid MatchEventId, int HomeScore, int AwayScore);
}

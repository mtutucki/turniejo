namespace Turniejo.Matches.Application.Matches.RevertMatchEvent
{
    public sealed record RevertMatchEventResponse(Guid MatchEventId, int HomeScore, int AwayScore);
}

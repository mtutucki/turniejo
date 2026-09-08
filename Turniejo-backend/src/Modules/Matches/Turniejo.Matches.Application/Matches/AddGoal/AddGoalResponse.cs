namespace Turniejo.Matches.Application.Matches.AddGoal
{
    public sealed record AddGoalResponse(Guid MatchEventId, int HomeScore, int AwayScore);
}

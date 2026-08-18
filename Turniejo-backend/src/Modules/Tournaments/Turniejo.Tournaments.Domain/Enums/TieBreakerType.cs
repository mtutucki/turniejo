namespace Turniejo.Tournaments.Domain.Enums
{
    public enum TieBreakerType
    {
        HeadToHeadPoints = 1,
        HeadToHeadGoalDiff = 2,
        GoalDifference = 3,
        GoalsFor = 4,
        Wins = 5,
        ManualDecision = 6,
        RandomDraw = 7
    }
}

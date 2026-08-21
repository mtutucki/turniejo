using Turniejo.SharedKernel.Results;

namespace Turniejo.Tournaments.Domain.Entities;

public sealed class TournamentSettings
{
    public Guid TournamentId { get; private set; }
    public int PointsForWin { get; private set; } = 3;
    public int PointsForDraw { get; private set; } = 1;
    public int PointsForLoss { get; private set; } = 0;
    public int MatchDurationMinutes { get; private set; } = 30;
    public int BreakDurationMinutes { get; private set; } = 5;
    public bool IsThirdPlaceMatchEnabled { get; private set; } = false;
    public int RefereeEditWindowMinutes { get; private set; } = 60;
    public int WalkoverHomeScore { get; private set; } = 3;
    public int WalkoverAwayScore { get; private set; } = 0;
    public bool CanCoachEditSquadAfterStart { get; private set; } = false;

    private TournamentSettings() { }

    public static TournamentSettings CreateDefault(Guid tournamentId)
    {
        var tournamentSettings = new TournamentSettings
        {
            TournamentId = tournamentId
        };

        return tournamentSettings;
    }

    public Result Update(int pointsForWin, int pointsForDraw, int pointsForLoss, int matchDurationMinutes, int breakDurationMinutes,
        bool isThirdPlaceMatchEnabled, int refereeEditWindowMinutes, int walkoverHomeScore, int walkoverAwayScore, bool canCoachEditSquadAfterStart)
    {
        if (pointsForWin < pointsForDraw)
            return Result.Failure(Error.Validation("PointsForWin", "PointsForWin cannot be less than pointsForDraw"));

        if (pointsForDraw < pointsForLoss)
            return Result.Failure(Error.Validation("PointsForDraw", "pointsForDraw cannot be less than pointsForLoss"));

        if (pointsForLoss < 0)
            return Result.Failure(Error.Validation("PointsForLoss", "pointsForLoss cannot be negative"));

        if (matchDurationMinutes <= 0)
            return Result.Failure(Error.Validation("MatchDurationMinutes", "matchDurationMinutes must be higher than 0"));

        if (walkoverHomeScore == walkoverAwayScore)
            return Result.Failure(Error.Validation("WalkoverHomeScore", "walkoverHomeScore cannot equal to walkoverAwayScore"));

        if (breakDurationMinutes < 0)
            return Result.Failure(Error.Validation("BreakDurationMinutes", "breakDurationMinutes must be higher than 0"));
        if (refereeEditWindowMinutes < 0)
            return Result.Failure(Error.Validation("RefereeEditWindowMinutes", "refereeEditWindowMinutes must be higher than 0"));

        PointsForWin = pointsForWin;
        PointsForDraw = pointsForDraw;
        PointsForLoss = pointsForLoss;
        MatchDurationMinutes = matchDurationMinutes;
        BreakDurationMinutes = breakDurationMinutes;
        WalkoverHomeScore = walkoverHomeScore;
        WalkoverAwayScore = walkoverAwayScore;
        RefereeEditWindowMinutes = refereeEditWindowMinutes;
        IsThirdPlaceMatchEnabled = isThirdPlaceMatchEnabled;
        CanCoachEditSquadAfterStart = canCoachEditSquadAfterStart;

        return Result.Success();
    }
}



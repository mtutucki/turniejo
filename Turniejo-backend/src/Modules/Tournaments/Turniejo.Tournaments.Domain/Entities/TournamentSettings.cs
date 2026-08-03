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
}



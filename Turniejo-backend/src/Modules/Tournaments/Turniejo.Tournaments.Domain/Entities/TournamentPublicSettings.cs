namespace Turniejo.Tournaments.Domain.Entities;

public sealed class TournamentPublicSettings
{
    public Guid TournamentId { get; private set; }
    public bool IsPublic { get; private set; } = false;
    public bool ShowTeams { get; private set; } = true;
    public bool ShowPlayers { get; private set; } = true;
    public bool ShowSchedule { get; private set; } = true;
    public bool ShowStandings { get; private set; } = true;
    public bool ShowBracket { get; private set; } = true;
    public bool ShowLiveMatches { get; private set; } = true;

    private TournamentPublicSettings() { }

    public static TournamentPublicSettings CreateDefault(Guid tournamentId)
    {
        var tournamentPublicSettings = new TournamentPublicSettings
        {
            TournamentId = tournamentId
        };
        return tournamentPublicSettings;
    }
}


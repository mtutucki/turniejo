using Turniejo.SharedKernel.Results;

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
    public bool ShowStats { get; private set; } = true;
    // RODO: zawodnicy to zwykle dzieci — portal domyślnie pokazuje "Jan K." zamiast pełnego nazwiska.
    public bool AbbreviatePlayerNames { get; private set; } = true;
    /// <summary>Publiczny turniej widać na liście turniejów (strona główna). Wyłączony: portal tylko przez link.</summary>
    public bool ShowInDirectory { get; private set; } = true;

    private TournamentPublicSettings() { }

    public static TournamentPublicSettings CreateDefault(Guid tournamentId)
    {
        var tournamentPublicSettings = new TournamentPublicSettings
        {
            TournamentId = tournamentId
        };
        return tournamentPublicSettings;
    }

    public Result Update(
        bool isPublic, bool showTeams, bool showPlayers, bool showSchedule,
        bool showStandings, bool showBracket, bool showLiveMatches, bool showStats,
        bool abbreviatePlayerNames = true, bool showInDirectory = true)
    {
        IsPublic = isPublic;
        ShowTeams = showTeams;
        ShowPlayers = showPlayers;
        ShowSchedule = showSchedule;
        ShowStandings = showStandings;
        ShowBracket = showBracket;
        ShowLiveMatches = showLiveMatches;
        ShowStats = showStats;
        AbbreviatePlayerNames = abbreviatePlayerNames;
        ShowInDirectory = showInDirectory;

        return Result.Success();
    }
}


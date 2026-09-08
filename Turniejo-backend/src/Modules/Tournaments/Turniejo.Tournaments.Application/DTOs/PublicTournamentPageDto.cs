namespace Turniejo.Tournaments.Application.DTOs
{
    public sealed record PublicTournamentSettingsDto(
        bool ShowTeams,
        bool ShowPlayers,
        bool ShowSchedule,
        bool ShowStandings,
        bool ShowBracket,
        bool ShowLiveMatches);

    public sealed record PublicTournamentPageDto(
        Guid Id,
        string Name,
        string Slug,
        string Status,
        string? Format,
        DateTimeOffset StartAt,
        DateTimeOffset EndAt,
        string? Description,
        PublicTournamentSettingsDto Settings);
}

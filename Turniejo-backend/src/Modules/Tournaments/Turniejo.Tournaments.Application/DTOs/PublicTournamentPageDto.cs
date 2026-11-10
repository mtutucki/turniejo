namespace Turniejo.Tournaments.Application.DTOs
{
    public sealed record PublicTournamentSettingsDto(
        bool ShowTeams,
        bool ShowPlayers,
        bool ShowSchedule,
        bool ShowStandings,
        bool ShowBracket,
        bool ShowLiveMatches,
        bool ShowStats);

    public sealed record PublicCategoryDto(Guid Id, string Name, int Order);
    public sealed record PublicGroupDto(Guid Id, Guid CategoryId, string Name, int Order, int Stage = 1);
    public sealed record PublicPitchDto(Guid Id, string Name);

    public sealed record PublicTournamentPageDto(
        Guid Id,
        string Name,
        string Slug,
        string Status,
        string? Format,
        DateTimeOffset StartAt,
        DateTimeOffset EndAt,
        string? Description,
        PublicTournamentSettingsDto Settings,
        // Nazwy do filtrów portalu ("Grupa A", "Boisko 1") oraz id grup/kategorii, których wymagają
        // publiczne tabele (groupId) i drabinka (categoryId) — publiczny terminarz zwraca same id.
        IReadOnlyList<PublicCategoryDto> Categories,
        IReadOnlyList<PublicGroupDto> Groups,
        IReadOnlyList<PublicPitchDto> Pitches);
}

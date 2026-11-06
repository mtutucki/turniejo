namespace Turniejo.Players.Application.DTOs
{
    public sealed record TeamRosterDto(
        Guid TeamId,
        string TeamName,
        bool CanEdit,
        IReadOnlyList<TeamRosterPlayerDto> Players);

    public sealed record TeamRosterPlayerDto(
        Guid PlayerId,
        string FirstName,
        string LastName,
        DateTime BirthDate,
        string DefaultPosition,
        string? PhotoUrl,
        int? ShirtNumber,
        // "Active" | "Left"
        string Status,
        DateTimeOffset? LeftAt,
        // Składy w turniejach, które jeszcze trwają albo się nie zaczęły. O nie pytamy przy usuwaniu z kadry.
        IReadOnlyList<TeamRosterSquadDto> CurrentSquads,
        // Zakończone turnieje, w których grał dla tej drużyny. Ta historia zostaje także po odejściu.
        int PastTournamentsCount);

    public sealed record TeamRosterSquadDto(
        Guid TournamentTeamId,
        Guid TournamentId,
        string TournamentName,
        string TournamentStatus,
        DateTimeOffset? TournamentStartAt,
        // Czy usuwający może zmienić ten skład (np. trener po starcie turnieju z blokadą składu nie może).
        bool CanEditSquad);
}

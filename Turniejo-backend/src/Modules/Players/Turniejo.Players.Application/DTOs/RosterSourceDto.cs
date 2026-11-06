namespace Turniejo.Players.Application.DTOs
{
    // Skąd można wziąć zawodników do bieżącego składu: kadra drużyny (Kind = "Roster", bez turnieju)
    // albo poprzednie zgłoszenie tej samej drużyny (Kind = "Tournament").
    public sealed record RosterSourceDto(
        Guid? TournamentTeamId,
        Guid? TournamentId,
        string TournamentName,
        DateTimeOffset? TournamentStartAt,
        string? CategoryName,
        IReadOnlyList<RosterSourcePlayerDto> Players,
        string Kind = RosterSourceDto.TournamentKind)
    {
        public const string RosterKind = "Roster";
        public const string TournamentKind = "Tournament";
    }

    public sealed record RosterSourcePlayerDto(
        Guid PlayerId,
        string FirstName,
        string LastName,
        DateTime BirthDate,
        int? ShirtNumber,
        string? Position,
        // Liczone względem drużyny docelowej: zawodnik już w jej składzie / poza rocznikiem jej kategorii.
        bool AlreadyInSquad,
        // null | "TooOld" | "TooYoung"
        string? Ineligibility);
}

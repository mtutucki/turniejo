namespace Turniejo.Tournaments.Application.DTOs
{
    public sealed record TournamentDto(
    Guid Id,
    string Name,
    string Slug,
    string Status,
    string? Format,
    DateTimeOffset StartAt,
    DateTimeOffset EndAt,
    int TeamLimit,
    bool IsPaid,
    // Kody dają rolę w turnieju (JoinTournament) — null dla każdego, kto nie jest jego organizatorem.
    // Tournament.OrganizerCode celowo nie jest wystawiany: JoinTournament go nie obsługuje.
    string? RefereeCode,
    string? CoachCode,
    string? ViewerCode,
    string? Description,
    // Czy portal kibica (/t/{slug}) jest włączony — bez tego link prowadzi na 404.
    bool IsPublic
    );
}

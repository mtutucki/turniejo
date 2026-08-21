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
    string OrganizerCode,
    string RefereeCode,
    string CoachCode,
    string ViewerCode,
    string? Description
    );
}

namespace Turniejo.Matches.Application.DTOs
{
    public sealed record PitchDto(
        Guid Id,
        Guid TournamentId,
        string Name,
        string? Description,
        bool IsActive,
        IReadOnlyList<Guid> GroupIds,
        Guid? RefereeId);
}

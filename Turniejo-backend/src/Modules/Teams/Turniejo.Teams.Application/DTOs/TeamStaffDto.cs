namespace Turniejo.Teams.Application.DTOs
{
    public sealed record TeamStaffDto(
        Guid Id,
        Guid TournamentTeamId,
        Guid UserId,
        string Role);
}

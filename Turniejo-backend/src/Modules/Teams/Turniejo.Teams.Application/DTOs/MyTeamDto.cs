namespace Turniejo.Teams.Application.DTOs
{
    public sealed record MyTeamDto(
        Guid TournamentTeamId,
        Guid TournamentId,
        string TournamentName,
        Guid TeamId,
        string TeamName,
        Guid CategoryId,
        Guid? GroupId,
        string Role,
        string TournamentStatus = "",
        bool CanEditSquad = true);
}

namespace Turniejo.Teams.Application.DTOs
{
    public sealed record PublicTeamDto(
        Guid TournamentTeamId,
        Guid TeamId,
        string Name,
        string? ShortName,
        Guid CategoryId,
        Guid? GroupId,
        string? LogoUrl = null);
}

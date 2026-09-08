using Turniejo.SharedKernel.Contracts;

namespace Turniejo.Players.Application.DTOs
{
    public sealed record MyChildStandingDto(
        Guid TournamentTeamId,
        Guid GroupId,
        IReadOnlyList<GroupStandingDto> Standings);
}

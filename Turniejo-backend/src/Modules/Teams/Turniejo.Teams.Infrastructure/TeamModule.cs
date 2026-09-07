using Turniejo.SharedKernel.Contracts;
using Turniejo.Teams.Application.Abstractions;

namespace Turniejo.Teams.Infrastructure;

internal sealed class TeamModule(ITeamsDbContext dbContext) : ITeamModule
{
    public async Task<TeamDto?> GetTeamAsync(Guid teamId, CancellationToken cancellationToken = default)
    {
        var team = await dbContext.GetTeamByIdAsync(teamId, cancellationToken);
        if (team is null)
            return null;

        return new TeamDto(team.Id, team.Name, team.ShortName);
    }
}

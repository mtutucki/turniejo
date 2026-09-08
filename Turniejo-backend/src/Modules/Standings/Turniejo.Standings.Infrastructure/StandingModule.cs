using Turniejo.SharedKernel.Contracts;
using Turniejo.Standings.Application.Abstractions;

namespace Turniejo.Standings.Infrastructure;

internal sealed class StandingModule(IStandingsDbContext dbContext) : IStandingModule
{
    public async Task<IReadOnlyList<GroupStandingDto>> GetGroupStandingsAsync(Guid groupId, CancellationToken cancellationToken = default)
    {
        var standings = await dbContext.GetStandingsByGroupIdAsync(groupId, cancellationToken);

        return standings
            .OrderBy(s => s.Position)
            .Select(s => new GroupStandingDto(s.TournamentTeamId, s.Position, s.Points))
            .ToList();
    }
}

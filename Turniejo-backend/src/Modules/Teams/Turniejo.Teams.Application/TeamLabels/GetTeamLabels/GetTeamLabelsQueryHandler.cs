using Turniejo.SharedKernel.CQRS;
using Turniejo.SharedKernel.Results;
using Turniejo.Teams.Application.Abstractions;
using Turniejo.Teams.Application.DTOs;

namespace Turniejo.Teams.Application.TeamLabels.GetTeamLabels
{
    internal sealed class GetTeamLabelsQueryHandler(ITeamsDbContext dbContext) : IQueryHandler<GetTeamLabelsQuery, IReadOnlyList<TeamLabelDto>>
    {
        public async Task<Result<IReadOnlyList<TeamLabelDto>>> Handle(GetTeamLabelsQuery request, CancellationToken cancellationToken)
        {
            var labels = await dbContext.GetTeamLabelsByOrganizationIdAsync(request.OrganizationId, cancellationToken);
            var teams = await dbContext.GetTeamsByOrganizationIdAsync(request.OrganizationId, cancellationToken);

            var counts = teams
                .SelectMany(t => t.LabelIds)
                .GroupBy(id => id)
                .ToDictionary(g => g.Key, g => g.Count());

            var dtos = labels
                .Select(l => new TeamLabelDto(l.Id, l.Name, counts.GetValueOrDefault(l.Id)))
                .ToList();

            return Result.Success<IReadOnlyList<TeamLabelDto>>(dtos);
        }
    }
}

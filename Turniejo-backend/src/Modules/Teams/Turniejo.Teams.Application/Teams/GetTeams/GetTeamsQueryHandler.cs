using Turniejo.SharedKernel.CQRS;
using Turniejo.SharedKernel.Results;
using Turniejo.Teams.Application.Abstractions;
using Turniejo.Teams.Application.DTOs;

namespace Turniejo.Teams.Application.Teams.GetTeams
{
    internal sealed class GetTeamsQueryHandler(ITeamsDbContext dbContext) : IQueryHandler<GetTeamsQuery, IReadOnlyList<TeamDto>>
    {
        public async Task<Result<IReadOnlyList<TeamDto>>> Handle(GetTeamsQuery request, CancellationToken cancellationToken)
        {
            var teams = await dbContext.GetTeamsByOrganizationIdAsync(request.OrganizationId, cancellationToken);

            var dtos = teams
                .Select(t => new TeamDto(t.Id, t.OrganizationId, t.Name, t.ShortName, t.LogoUrl))
                .ToList();

            return Result.Success<IReadOnlyList<TeamDto>>(dtos);
        }
    }
}

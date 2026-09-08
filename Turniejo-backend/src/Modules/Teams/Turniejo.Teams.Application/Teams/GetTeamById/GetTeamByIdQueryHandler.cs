using Turniejo.SharedKernel.CQRS;
using Turniejo.SharedKernel.Results;
using Turniejo.Teams.Application.Abstractions;
using Turniejo.Teams.Application.DTOs;

namespace Turniejo.Teams.Application.Teams.GetTeamById
{
    internal sealed class GetTeamByIdQueryHandler(ITeamsDbContext dbContext) : IQueryHandler<GetTeamByIdQuery, TeamDto>
    {
        public async Task<Result<TeamDto>> Handle(GetTeamByIdQuery request, CancellationToken cancellationToken)
        {
            var team = await dbContext.GetTeamByIdAsync(request.TeamId, cancellationToken);
            if (team == null || team.OrganizationId != request.OrganizationId)
                return Result.Failure<TeamDto>(Error.NotFound("Team", request.TeamId));

            var dto = new TeamDto(team.Id, team.OrganizationId, team.Name, team.ShortName, team.LogoUrl);

            return Result.Success(dto);
        }
    }
}

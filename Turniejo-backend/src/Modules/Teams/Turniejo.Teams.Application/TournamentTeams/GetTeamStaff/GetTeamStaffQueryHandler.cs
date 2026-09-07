using Turniejo.SharedKernel.CQRS;
using Turniejo.SharedKernel.Results;
using Turniejo.Teams.Application.Abstractions;
using Turniejo.Teams.Application.DTOs;

namespace Turniejo.Teams.Application.TournamentTeams.GetTeamStaff
{
    internal sealed class GetTeamStaffQueryHandler(ITeamsDbContext dbContext) : IQueryHandler<GetTeamStaffQuery, IReadOnlyList<TeamStaffDto>>
    {
        public async Task<Result<IReadOnlyList<TeamStaffDto>>> Handle(GetTeamStaffQuery request, CancellationToken cancellationToken)
        {
            var staff = await dbContext.GetTeamStaffByTournamentTeamIdAsync(request.TournamentTeamId, cancellationToken);

            var dtos = staff.Select(s => new TeamStaffDto(s.Id, s.TournamentTeamId, s.UserId, s.Role.ToString())).ToList();

            return Result.Success<IReadOnlyList<TeamStaffDto>>(dtos);
        }
    }
}

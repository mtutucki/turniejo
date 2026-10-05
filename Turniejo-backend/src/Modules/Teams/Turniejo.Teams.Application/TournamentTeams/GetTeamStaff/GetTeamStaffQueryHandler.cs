using Turniejo.SharedKernel.Contracts;
using Turniejo.SharedKernel.CQRS;
using Turniejo.SharedKernel.Results;
using Turniejo.Teams.Application.Abstractions;
using Turniejo.Teams.Application.DTOs;

namespace Turniejo.Teams.Application.TournamentTeams.GetTeamStaff
{
    internal sealed class GetTeamStaffQueryHandler(ITeamsDbContext dbContext, IUserModule userModule) : IQueryHandler<GetTeamStaffQuery, IReadOnlyList<TeamStaffDto>>
    {
        public async Task<Result<IReadOnlyList<TeamStaffDto>>> Handle(GetTeamStaffQuery request, CancellationToken cancellationToken)
        {
            var staff = await dbContext.GetTeamStaffByTournamentTeamIdAsync(request.TournamentTeamId, cancellationToken);

            // Staff drużyny to kilka osób — pojedyncze odczyty z modułu Users są tu wystarczające.
            var usersById = new Dictionary<Guid, UserDto?>();
            foreach (var userId in staff.Select(s => s.UserId).Distinct())
                usersById[userId] = await userModule.GetUserAsync(userId, cancellationToken);

            var dtos = staff.Select(s =>
            {
                var user = usersById.GetValueOrDefault(s.UserId);
                return new TeamStaffDto(s.Id, s.TournamentTeamId, s.UserId, s.Role.ToString(),
                    user?.Email, user?.FirstName, user?.LastName);
            }).ToList();

            return Result.Success<IReadOnlyList<TeamStaffDto>>(dtos);
        }
    }
}

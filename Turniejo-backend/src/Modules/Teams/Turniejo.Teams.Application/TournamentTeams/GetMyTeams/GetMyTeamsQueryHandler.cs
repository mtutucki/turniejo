using Turniejo.SharedKernel.Contracts;
using Turniejo.SharedKernel.CQRS;
using Turniejo.SharedKernel.Results;
using Turniejo.Teams.Application.Abstractions;
using Turniejo.Teams.Application.DTOs;

namespace Turniejo.Teams.Application.TournamentTeams.GetMyTeams
{
    internal sealed class GetMyTeamsQueryHandler(
        ITeamsDbContext dbContext,
        ITournamentModule tournamentModule,
        ICurrentUserContext currentUserContext) : IQueryHandler<GetMyTeamsQuery, IReadOnlyList<MyTeamDto>>
    {
        public async Task<Result<IReadOnlyList<MyTeamDto>>> Handle(GetMyTeamsQuery request, CancellationToken cancellationToken)
        {
            var staffAssignments = await dbContext.GetTeamStaffByUserIdAsync(currentUserContext.UserId, cancellationToken);

            var dtos = new List<MyTeamDto>();
            foreach (var staff in staffAssignments)
            {
                var tournamentTeam = await dbContext.GetTournamentTeamByIdAsync(staff.TournamentTeamId, cancellationToken);
                if (tournamentTeam == null)
                    continue;

                var team = await dbContext.GetTeamByIdAsync(tournamentTeam.TeamId, cancellationToken);
                var tournament = await tournamentModule.GetTournamentAsync(tournamentTeam.TournamentId, cancellationToken);

                dtos.Add(new MyTeamDto(
                    tournamentTeam.Id, tournamentTeam.TournamentId, tournament?.Name ?? "Unknown",
                    tournamentTeam.TeamId, team?.Name ?? "Unknown", tournamentTeam.CategoryId,
                    tournamentTeam.GroupId, staff.Role.ToString()));
            }

            return Result.Success<IReadOnlyList<MyTeamDto>>(dtos);
        }
    }
}

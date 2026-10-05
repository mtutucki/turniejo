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
                var settings = await tournamentModule.GetTournamentSettingsAsync(tournamentTeam.TournamentId, cancellationToken);

                // Ta sama reguła co SquadEditGuard — trener nie ma dostępu do ustawień turnieju,
                // więc panel dostaje gotową flagę zamiast liczyć ją po swojej stronie.
                var hasStarted = tournament?.Status is "InProgress" or "Finished";
                var canEditSquad = tournament != null && settings != null
                    && (!hasStarted || settings.CanCoachEditSquadAfterStart);

                dtos.Add(new MyTeamDto(
                    tournamentTeam.Id, tournamentTeam.TournamentId, tournament?.Name ?? "Unknown",
                    tournamentTeam.TeamId, team?.Name ?? "Unknown", tournamentTeam.CategoryId,
                    tournamentTeam.GroupId, staff.Role.ToString(),
                    tournament?.Status ?? "", canEditSquad));
            }

            return Result.Success<IReadOnlyList<MyTeamDto>>(dtos);
        }
    }
}

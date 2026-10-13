using Turniejo.SharedKernel.Contracts;
using Turniejo.SharedKernel.CQRS;
using Turniejo.SharedKernel.Enums;
using Turniejo.SharedKernel.Results;
using Turniejo.Teams.Application.Abstractions;

namespace Turniejo.Teams.Application.TournamentTeams.RemoveStaffFromTeam
{
    internal sealed class RemoveStaffFromTeamCommandHandler(
        ITeamsDbContext dbContext,
        IUserModule userModule) : ICommandHandler<RemoveStaffFromTeamCommand, RemoveStaffFromTeamResponse>
    {
        public async Task<Result<RemoveStaffFromTeamResponse>> Handle(RemoveStaffFromTeamCommand request, CancellationToken cancellationToken)
        {
            // AuthorizationBehavior potwierdził tylko rolę organizatora w request.TournamentId — drużyna
            // i wpis sztabu pochodzą od klienta, więc sprawdzamy, że faktycznie do siebie należą.
            var tournamentTeam = await dbContext.GetTournamentTeamByIdAsync(request.TournamentTeamId, cancellationToken);
            if (tournamentTeam == null || tournamentTeam.TournamentId != request.TournamentId)
                return Result.Failure<RemoveStaffFromTeamResponse>(Error.NotFound("TournamentTeam", request.TournamentTeamId));

            var staff = (await dbContext.GetTeamStaffByTournamentTeamIdAsync(request.TournamentTeamId, cancellationToken))
                .FirstOrDefault(s => s.Id == request.TeamStaffId);
            if (staff == null)
                return Result.Failure<RemoveStaffFromTeamResponse>(Error.NotFound("TeamStaff", request.TeamStaffId));

            await dbContext.RemoveTeamStaffAsync(staff, cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);

            // Bez tego usunięta osoba zachowałaby rolę na drużynie, a z nią dostęp do składu i panelu trenera.
            await userModule.RevokeRoleAsync(
                staff.UserId,
                staff.Role.ToString(),
                nameof(RoleScopeType.TournamentTeam),
                request.TournamentTeamId,
                cancellationToken);

            return Result.Success(new RemoveStaffFromTeamResponse(staff.Id));
        }
    }
}

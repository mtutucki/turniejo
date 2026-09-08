using Turniejo.SharedKernel.CQRS;
using Turniejo.SharedKernel.Results;
using Turniejo.Teams.Application.Abstractions;

namespace Turniejo.Teams.Application.TournamentTeams.AssignTeamToGroup
{
    internal sealed class AssignTeamToGroupCommandHandler(ITeamsDbContext dbContext) : ICommandHandler<AssignTeamToGroupCommand, AssignTeamToGroupResponse>
    {
        public async Task<Result<AssignTeamToGroupResponse>> Handle(AssignTeamToGroupCommand request, CancellationToken cancellationToken)
        {
            var tournamentTeam = await dbContext.GetTournamentTeamByIdAsync(request.TournamentTeamId, cancellationToken);
            if (tournamentTeam == null || tournamentTeam.TournamentId != request.TournamentId)
                return Result.Failure<AssignTeamToGroupResponse>(Error.NotFound("TournamentTeam", request.TournamentTeamId));

            tournamentTeam.AssignToGroup(request.GroupId);

            await dbContext.SaveChangesAsync(cancellationToken);

            return Result.Success(new AssignTeamToGroupResponse(tournamentTeam.Id));
        }
    }
}

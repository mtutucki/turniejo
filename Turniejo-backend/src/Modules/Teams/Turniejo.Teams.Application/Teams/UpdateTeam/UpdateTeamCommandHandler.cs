using Turniejo.SharedKernel.CQRS;
using Turniejo.SharedKernel.Results;
using Turniejo.Teams.Application.Abstractions;

namespace Turniejo.Teams.Application.Teams.UpdateTeam
{
    internal sealed class UpdateTeamCommandHandler(ITeamsDbContext dbContext) : ICommandHandler<UpdateTeamCommand, UpdateTeamResponse>
    {
        public async Task<Result<UpdateTeamResponse>> Handle(UpdateTeamCommand request, CancellationToken cancellationToken)
        {
            var team = await dbContext.GetTeamByIdAsync(request.TeamId, cancellationToken);
            if (team == null || team.OrganizationId != request.OrganizationId)
                return Result.Failure<UpdateTeamResponse>(Error.NotFound("Team", request.TeamId));

            var updateResult = team.UpdateDetails(request.Name, request.ShortName);
            if (updateResult.IsFailure)
                return Result.Failure<UpdateTeamResponse>(updateResult.Error);

            await dbContext.SaveChangesAsync(cancellationToken);

            return Result.Success(new UpdateTeamResponse(team.Id));
        }
    }
}

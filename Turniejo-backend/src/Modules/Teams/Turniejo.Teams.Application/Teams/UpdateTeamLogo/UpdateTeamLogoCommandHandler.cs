using Turniejo.SharedKernel.CQRS;
using Turniejo.SharedKernel.Results;
using Turniejo.Teams.Application.Abstractions;

namespace Turniejo.Teams.Application.Teams.UpdateTeamLogo
{
    internal sealed class UpdateTeamLogoCommandHandler(ITeamsDbContext dbContext, ITeamLogoStorage logoStorage) : ICommandHandler<UpdateTeamLogoCommand, UpdateTeamLogoResponse>
    {
        public async Task<Result<UpdateTeamLogoResponse>> Handle(UpdateTeamLogoCommand request, CancellationToken cancellationToken)
        {
            var team = await dbContext.GetTeamByIdAsync(request.TeamId, cancellationToken);
            if (team == null || team.OrganizationId != request.OrganizationId)
                return Result.Failure<UpdateTeamLogoResponse>(Error.NotFound("Team", request.TeamId));

            var previousLogoUrl = team.LogoUrl;
            team.UpdateLogo(request.LogoUrl);

            await dbContext.SaveChangesAsync(cancellationToken);

            // Stary wgrany plik nie jest już nigdzie używany (każdy upload ma własną nazwę).
            if (previousLogoUrl != team.LogoUrl)
                logoStorage.DeleteIfStored(previousLogoUrl);

            return Result.Success(new UpdateTeamLogoResponse(team.Id));
        }
    }
}

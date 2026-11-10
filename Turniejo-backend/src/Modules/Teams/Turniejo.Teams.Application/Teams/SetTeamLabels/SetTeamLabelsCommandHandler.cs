using Turniejo.SharedKernel.CQRS;
using Turniejo.SharedKernel.Results;
using Turniejo.Teams.Application.Abstractions;
using Turniejo.Teams.Application.TeamLabels;

namespace Turniejo.Teams.Application.Teams.SetTeamLabels
{
    internal sealed class SetTeamLabelsCommandHandler(ITeamsDbContext dbContext) : ICommandHandler<SetTeamLabelsCommand>
    {
        public async Task<Result> Handle(SetTeamLabelsCommand request, CancellationToken cancellationToken)
        {
            var team = await dbContext.GetTeamByIdAsync(request.TeamId, cancellationToken);
            if (team == null || team.OrganizationId != request.OrganizationId)
                return Result.Failure(Error.NotFound("Team", request.TeamId));

            var labels = await dbContext.GetTeamLabelsByOrganizationIdAsync(request.OrganizationId, cancellationToken);
            if (!request.LabelIds.ToHashSet().IsSubsetOf(labels.Select(l => l.Id)))
                return Result.Failure(TeamLabelErrors.LabelOutsideOrganization);

            team.SetLabels(request.LabelIds);
            await dbContext.SaveChangesAsync(cancellationToken);
            return Result.Success();
        }
    }
}

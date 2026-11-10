using Turniejo.SharedKernel.CQRS;
using Turniejo.SharedKernel.Results;
using Turniejo.Teams.Application.Abstractions;

namespace Turniejo.Teams.Application.TeamLabels.DeleteTeamLabel
{
    internal sealed class DeleteTeamLabelCommandHandler(ITeamsDbContext dbContext) : ICommandHandler<DeleteTeamLabelCommand>
    {
        public async Task<Result> Handle(DeleteTeamLabelCommand request, CancellationToken cancellationToken)
        {
            var label = await dbContext.GetTeamLabelByIdAsync(request.LabelId, cancellationToken);
            if (label == null || label.OrganizationId != request.OrganizationId)
                return Result.Failure(Error.NotFound("TeamLabel", request.LabelId));

            foreach (var team in await dbContext.GetTeamsByOrganizationIdForUpdateAsync(request.OrganizationId, cancellationToken))
                team.RemoveLabel(label.Id);

            await dbContext.RemoveTeamLabelAsync(label, cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);
            return Result.Success();
        }
    }
}

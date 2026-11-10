using Turniejo.SharedKernel.CQRS;
using Turniejo.SharedKernel.Results;
using Turniejo.Teams.Application.Abstractions;

namespace Turniejo.Teams.Application.TeamLabels.RenameTeamLabel
{
    internal sealed class RenameTeamLabelCommandHandler(ITeamsDbContext dbContext) : ICommandHandler<RenameTeamLabelCommand>
    {
        public async Task<Result> Handle(RenameTeamLabelCommand request, CancellationToken cancellationToken)
        {
            var label = await dbContext.GetTeamLabelByIdAsync(request.LabelId, cancellationToken);
            if (label == null || label.OrganizationId != request.OrganizationId)
                return Result.Failure(Error.NotFound("TeamLabel", request.LabelId));

            if (await dbContext.TeamLabelNameExistsAsync(request.OrganizationId, request.Name.Trim(), request.LabelId, cancellationToken))
                return Result.Failure(TeamLabelErrors.DuplicateName);

            var result = label.Rename(request.Name);
            if (result.IsFailure)
                return result;

            await dbContext.SaveChangesAsync(cancellationToken);
            return Result.Success();
        }
    }
}

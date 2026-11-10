using Turniejo.SharedKernel.CQRS;
using Turniejo.SharedKernel.Results;
using Turniejo.Teams.Application.Abstractions;
using Turniejo.Teams.Domain.Entities;

namespace Turniejo.Teams.Application.TeamLabels.CreateTeamLabel
{
    internal sealed class CreateTeamLabelCommandHandler(ITeamsDbContext dbContext) : ICommandHandler<CreateTeamLabelCommand, CreateTeamLabelResponse>
    {
        public async Task<Result<CreateTeamLabelResponse>> Handle(CreateTeamLabelCommand request, CancellationToken cancellationToken)
        {
            if (await dbContext.TeamLabelNameExistsAsync(request.OrganizationId, request.Name.Trim(), exceptLabelId: null, cancellationToken))
                return Result.Failure<CreateTeamLabelResponse>(TeamLabelErrors.DuplicateName);

            var result = TeamLabel.Create(Guid.NewGuid(), request.OrganizationId, request.Name);
            if (result.IsFailure)
                return Result.Failure<CreateTeamLabelResponse>(result.Error);

            await dbContext.AddTeamLabelAsync(result.Value, cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);

            return Result.Success(new CreateTeamLabelResponse(result.Value.Id));
        }
    }
}

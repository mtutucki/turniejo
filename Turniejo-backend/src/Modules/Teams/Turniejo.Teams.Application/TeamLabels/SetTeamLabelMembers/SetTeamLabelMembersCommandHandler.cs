using Turniejo.SharedKernel.CQRS;
using Turniejo.SharedKernel.Results;
using Turniejo.Teams.Application.Abstractions;

namespace Turniejo.Teams.Application.TeamLabels.SetTeamLabelMembers
{
    internal sealed class SetTeamLabelMembersCommandHandler(ITeamsDbContext dbContext) : ICommandHandler<SetTeamLabelMembersCommand>
    {
        public async Task<Result> Handle(SetTeamLabelMembersCommand request, CancellationToken cancellationToken)
        {
            var label = await dbContext.GetTeamLabelByIdAsync(request.LabelId, cancellationToken);
            if (label == null || label.OrganizationId != request.OrganizationId)
                return Result.Failure(Error.NotFound("TeamLabel", request.LabelId));

            var teams = await dbContext.GetTeamsByOrganizationIdForUpdateAsync(request.OrganizationId, cancellationToken);
            var members = request.TeamIds.ToHashSet();
            // Drużyna spoza organizacji (albo nieistniejąca) odrzuca całą zmianę.
            if (!members.IsSubsetOf(teams.Select(t => t.Id)))
                return Result.Failure(TeamLabelErrors.TeamOutsideOrganization);

            foreach (var team in teams)
            {
                if (members.Contains(team.Id))
                    team.AddLabel(label.Id);
                else
                    team.RemoveLabel(label.Id);
            }

            await dbContext.SaveChangesAsync(cancellationToken);
            return Result.Success();
        }
    }
}

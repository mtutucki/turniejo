using FluentValidation;

namespace Turniejo.Teams.Application.TeamLabels.SetTeamLabelMembers
{
    public sealed class SetTeamLabelMembersCommandValidator : AbstractValidator<SetTeamLabelMembersCommand>
    {
        public SetTeamLabelMembersCommandValidator()
        {
            RuleFor(x => x.OrganizationId).NotEmpty().WithMessage("OrganizationId is required.");
            RuleFor(x => x.LabelId).NotEmpty().WithMessage("LabelId is required.");
            RuleFor(x => x.TeamIds).NotNull().WithMessage("TeamIds is required.");
        }
    }
}

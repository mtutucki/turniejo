using FluentValidation;

namespace Turniejo.Teams.Application.Teams.SetTeamLabels
{
    public sealed class SetTeamLabelsCommandValidator : AbstractValidator<SetTeamLabelsCommand>
    {
        public SetTeamLabelsCommandValidator()
        {
            RuleFor(x => x.OrganizationId).NotEmpty().WithMessage("OrganizationId is required.");
            RuleFor(x => x.TeamId).NotEmpty().WithMessage("TeamId is required.");
            RuleFor(x => x.LabelIds).NotNull().WithMessage("LabelIds is required.");
        }
    }
}

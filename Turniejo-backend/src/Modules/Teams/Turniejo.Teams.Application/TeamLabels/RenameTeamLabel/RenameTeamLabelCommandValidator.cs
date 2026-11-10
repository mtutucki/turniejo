using FluentValidation;
using Turniejo.Teams.Domain.Entities;

namespace Turniejo.Teams.Application.TeamLabels.RenameTeamLabel
{
    public sealed class RenameTeamLabelCommandValidator : AbstractValidator<RenameTeamLabelCommand>
    {
        public RenameTeamLabelCommandValidator()
        {
            RuleFor(x => x.OrganizationId).NotEmpty().WithMessage("OrganizationId is required.");
            RuleFor(x => x.LabelId).NotEmpty().WithMessage("LabelId is required.");
            RuleFor(x => x.Name)
                .Must(n => !string.IsNullOrWhiteSpace(n)).WithMessage("Label name is required.")
                .MaximumLength(TeamLabel.NameMaxLength).WithMessage("Label name must not exceed 60 characters.");
        }
    }
}

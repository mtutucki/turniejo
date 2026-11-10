using FluentValidation;
using Turniejo.Teams.Domain.Entities;

namespace Turniejo.Teams.Application.TeamLabels.CreateTeamLabel
{
    public sealed class CreateTeamLabelCommandValidator : AbstractValidator<CreateTeamLabelCommand>
    {
        public CreateTeamLabelCommandValidator()
        {
            RuleFor(x => x.OrganizationId).NotEmpty().WithMessage("OrganizationId is required.");
            RuleFor(x => x.Name)
                .Must(n => !string.IsNullOrWhiteSpace(n)).WithMessage("Label name is required.")
                .MaximumLength(TeamLabel.NameMaxLength).WithMessage("Label name must not exceed 60 characters.");
        }
    }
}

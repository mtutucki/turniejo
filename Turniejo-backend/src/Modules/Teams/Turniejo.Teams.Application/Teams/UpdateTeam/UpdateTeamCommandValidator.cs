using FluentValidation;

namespace Turniejo.Teams.Application.Teams.UpdateTeam
{
    public sealed class UpdateTeamCommandValidator : AbstractValidator<UpdateTeamCommand>
    {
        public UpdateTeamCommandValidator()
        {
            RuleFor(x => x.OrganizationId)
                .NotEmpty().WithMessage("OrganizationId is required.");
            RuleFor(x => x.TeamId)
                .NotEmpty().WithMessage("TeamId is required.");
            RuleFor(x => x.Name)
                .NotEmpty().WithMessage("Name is required.")
                .MaximumLength(200).WithMessage("Name must not exceed 200 characters.");
            RuleFor(x => x.ShortName)
                .MaximumLength(20).WithMessage("ShortName must not exceed 20 characters.");
        }
    }
}

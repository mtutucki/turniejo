using FluentValidation;

namespace Turniejo.Teams.Application.Teams.CreateTeam
{
    public sealed class CreateTeamCommandValidator : AbstractValidator<CreateTeamCommand>
    {
        public CreateTeamCommandValidator()
        {
            RuleFor(x => x.OrganizationId)
                .NotEmpty().WithMessage("OrganizationId is required.");
            RuleFor(x => x.Name)
                .NotEmpty().WithMessage("Name is required.")
                .MaximumLength(200).WithMessage("Name must not exceed 200 characters.");
            RuleFor(x => x.ShortName)
                .MaximumLength(20).WithMessage("ShortName must not exceed 20 characters.");
        }
    }
}

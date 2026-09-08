using FluentValidation;

namespace Turniejo.Teams.Application.Teams.UpdateTeamLogo
{
    public sealed class UpdateTeamLogoCommandValidator : AbstractValidator<UpdateTeamLogoCommand>
    {
        public UpdateTeamLogoCommandValidator()
        {
            RuleFor(x => x.OrganizationId)
                .NotEmpty().WithMessage("OrganizationId is required.");
            RuleFor(x => x.TeamId)
                .NotEmpty().WithMessage("TeamId is required.");
            RuleFor(x => x.LogoUrl)
                .MaximumLength(2048).WithMessage("LogoUrl must not exceed 2048 characters.");
        }
    }
}

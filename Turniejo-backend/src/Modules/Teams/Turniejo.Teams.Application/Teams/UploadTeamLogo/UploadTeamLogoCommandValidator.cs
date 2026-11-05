using FluentValidation;

namespace Turniejo.Teams.Application.Teams.UploadTeamLogo
{
    public sealed class UploadTeamLogoCommandValidator : AbstractValidator<UploadTeamLogoCommand>
    {
        public UploadTeamLogoCommandValidator()
        {
            RuleFor(x => x.OrganizationId)
                .NotEmpty().WithMessage("OrganizationId is required.");
            RuleFor(x => x.Content)
                .NotEmpty().WithMessage("Logo file is empty.")
                .Must(c => c.Length <= UploadTeamLogoCommand.MaxBytes).WithMessage("Logo file is too large.");
        }
    }
}

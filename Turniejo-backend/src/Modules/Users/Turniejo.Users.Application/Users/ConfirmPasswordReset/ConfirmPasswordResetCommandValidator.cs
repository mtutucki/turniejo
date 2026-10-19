using FluentValidation;

namespace Turniejo.Users.Application.Users.ConfirmPasswordReset;

public sealed class ConfirmPasswordResetCommandValidator
    : AbstractValidator<ConfirmPasswordResetCommand>
{
    public ConfirmPasswordResetCommandValidator()
    {
        RuleFor(x => x.Token)
            .NotEmpty().WithMessage("Token is required.")
            .MaximumLength(200);

        RuleFor(x => x.NewPassword).MeetsPasswordPolicy();
    }
}

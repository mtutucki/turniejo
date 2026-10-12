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

        // Limity 1:1 z RegisterUserCommandValidator (i z passwordPolicy realmu w Keycloaku).
        RuleFor(x => x.NewPassword)
            .NotEmpty().WithMessage("Password is required.")
            .MinimumLength(8).WithMessage("Password must be at least 8 characters.")
            .MaximumLength(100);
    }
}

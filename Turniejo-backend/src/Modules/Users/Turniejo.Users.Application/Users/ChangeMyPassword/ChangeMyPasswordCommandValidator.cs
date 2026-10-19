using FluentValidation;

namespace Turniejo.Users.Application.Users.ChangeMyPassword;

public sealed class ChangeMyPasswordCommandValidator : AbstractValidator<ChangeMyPasswordCommand>
{
    public ChangeMyPasswordCommandValidator()
    {
        RuleFor(x => x.CurrentPassword)
            .NotEmpty().WithMessage("Current password is required.")
            .MaximumLength(PasswordPolicy.MaxLength);

        RuleFor(x => x.NewPassword)
            .MeetsPasswordPolicy()
            .NotEqual(x => x.CurrentPassword).WithMessage("New password must be different from the current one.");
    }
}

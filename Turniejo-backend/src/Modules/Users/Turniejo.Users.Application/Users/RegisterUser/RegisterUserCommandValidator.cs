using FluentValidation;

namespace Turniejo.Users.Application.Users.RegisterUser;

public sealed class RegisterUserCommandValidator : AbstractValidator<RegisterUserCommand>
{
    public RegisterUserCommandValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email is required.")
            .EmailAddress().WithMessage("Email is not valid.")
            .MaximumLength(256);

        RuleFor(x => x.Password).MeetsPasswordPolicy();

        RuleFor(x => x.FirstName)
            .NotEmpty().WithMessage("First name is required.")
            .MaximumLength(100);

        RuleFor(x => x.LastName)
            .NotEmpty().WithMessage("Last name is required.")
            .MaximumLength(100);

        RuleFor(x => x.AcceptTerms)
            .Equal(true).WithMessage("You must accept the terms and privacy policy.");

        RuleFor(x => x.PhoneNumber)
            .MaximumLength(20)
            .When(x => x.PhoneNumber is not null);

        // Limit jak w CreateOrganizationCommandValidator — inaczej zbyt długa nazwa wywracałaby dopiero outbox.
        RuleFor(x => x.OrganizationName)
            .MaximumLength(200)
            .When(x => x.OrganizationName is not null);
    }
}
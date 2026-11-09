using FluentValidation;

namespace Turniejo.Users.Application.Users.MarkReleaseSeen;

// Format sprawdza też User.MarkReleaseSeen. Limit długości jak kolumna (20).
public sealed class MarkReleaseSeenCommandValidator : AbstractValidator<MarkReleaseSeenCommand>
{
    public MarkReleaseSeenCommandValidator()
    {
        RuleFor(x => x.Version)
            .NotEmpty().WithMessage("Release version is required.")
            .MaximumLength(20)
            .Matches(@"^\d{1,5}\.\d{1,5}\.\d{1,5}$").WithMessage("Release version must look like 1.2.0.");
    }
}

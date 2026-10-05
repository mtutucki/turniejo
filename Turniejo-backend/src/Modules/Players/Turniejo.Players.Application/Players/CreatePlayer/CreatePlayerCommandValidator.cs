using FluentValidation;

namespace Turniejo.Players.Application.Players.CreatePlayer
{
    public sealed class CreatePlayerCommandValidator : AbstractValidator<CreatePlayerCommand>
    {
        public CreatePlayerCommandValidator()
        {
            RuleFor(x => x.FirstName)
                .NotEmpty().WithMessage("FirstName is required.")
                .MaximumLength(100).WithMessage("FirstName must not exceed 100 characters.");
            RuleFor(x => x.LastName)
                .NotEmpty().WithMessage("LastName is required.")
                .MaximumLength(100).WithMessage("LastName must not exceed 100 characters.");
            RuleFor(x => x.BirthDate)
                .LessThanOrEqualTo(DateTime.UtcNow.Date).WithMessage("BirthDate cannot be in the future.");
            RuleFor(x => x.PhotoUrl)
                .MaximumLength(2048).WithMessage("PhotoUrl must not exceed 2048 characters.")
                .Must(BeAValidHttpUrl).WithMessage("PhotoUrl must be a valid absolute http(s) URL.")
                .When(x => !string.IsNullOrEmpty(x.PhotoUrl));
        }

        private static bool BeAValidHttpUrl(string? url) =>
            Uri.TryCreate(url, UriKind.Absolute, out var uri) &&
            (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
    }
}

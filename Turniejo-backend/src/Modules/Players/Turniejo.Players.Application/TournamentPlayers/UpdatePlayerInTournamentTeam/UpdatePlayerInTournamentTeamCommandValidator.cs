using FluentValidation;

namespace Turniejo.Players.Application.TournamentPlayers.UpdatePlayerInTournamentTeam
{
    // Reguły profilu 1:1 z UpdatePlayerCommandValidator, reguły składu z UpdateTournamentPlayerCommandValidator.
    public sealed class UpdatePlayerInTournamentTeamCommandValidator : AbstractValidator<UpdatePlayerInTournamentTeamCommand>
    {
        public UpdatePlayerInTournamentTeamCommandValidator()
        {
            RuleFor(x => x.TournamentId)
                .NotEmpty().WithMessage("TournamentId is required.");
            RuleFor(x => x.TournamentTeamId)
                .NotEmpty().WithMessage("TournamentTeamId is required.");
            RuleFor(x => x.TournamentPlayerId)
                .NotEmpty().WithMessage("TournamentPlayerId is required.");
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
            RuleFor(x => x.ShirtNumber)
                .GreaterThan(0).When(x => x.ShirtNumber.HasValue).WithMessage("ShirtNumber must be greater than 0.");
            RuleFor(x => x.Status)
                .IsInEnum().WithMessage("Status is not a valid tournament player status.");
        }

        private static bool BeAValidHttpUrl(string? url) =>
            Uri.TryCreate(url, UriKind.Absolute, out var uri) &&
            (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
    }
}

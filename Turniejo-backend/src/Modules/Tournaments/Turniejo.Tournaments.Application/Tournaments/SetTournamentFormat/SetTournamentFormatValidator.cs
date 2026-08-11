using FluentValidation;

namespace Turniejo.Tournaments.Application.Tournaments.SetTournamentFormat
{
    public sealed class SetTournamentFormatValidator : AbstractValidator<SetTournamentFormatCommand>
    {
        public SetTournamentFormatValidator()
        {
            RuleFor(x => x.TournamentId)
                .NotEmpty().WithMessage("Tournament Id cannot be empty");
            RuleFor(x => x.TournamentFormat)
                .IsInEnum().WithMessage("Invalid tournament format");
        }
    }
}

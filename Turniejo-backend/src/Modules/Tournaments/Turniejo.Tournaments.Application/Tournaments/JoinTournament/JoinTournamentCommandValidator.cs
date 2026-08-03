using FluentValidation;

namespace Turniejo.Tournaments.Application.Tournaments.JoinTournament
{
    public sealed class JoinTournamentCommandValidator : AbstractValidator<JoinTournamentCommand>
    {
        public JoinTournamentCommandValidator()
        {
            RuleFor(x => x.Code)
                .NotEmpty().WithMessage("Tournament code is required.")
                .MaximumLength(10).WithMessage("Tournament code must be max 10 characters long.");
        }
    }
}

using FluentValidation;

namespace Turniejo.Tournaments.Application.Tournaments.ChangeTournamentStatus
{
    public sealed class ChangeTournamentStatusValidator : AbstractValidator<ChangeTournamentStatusCommand>
    {
        public ChangeTournamentStatusValidator()
        {
            RuleFor(x => x.TournamentId)
                .NotEmpty().WithMessage("TournamentId is required.");

            RuleFor(x => x.TournamentStatus)
                .IsInEnum().WithMessage("TournamentStatus must be a valid enum value.");
        }
    }
}

using FluentValidation;

namespace Turniejo.Matches.Application.Matches.PauseMatch
{
    public sealed class PauseMatchCommandValidator : AbstractValidator<PauseMatchCommand>
    {
        public PauseMatchCommandValidator()
        {
            RuleFor(x => x.TournamentId)
                .NotEmpty().WithMessage("TournamentId is required.");
            RuleFor(x => x.MatchId)
                .NotEmpty().WithMessage("MatchId is required.");
        }
    }
}

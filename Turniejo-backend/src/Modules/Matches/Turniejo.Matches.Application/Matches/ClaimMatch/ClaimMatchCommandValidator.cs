using FluentValidation;

namespace Turniejo.Matches.Application.Matches.ClaimMatch
{
    public sealed class ClaimMatchCommandValidator : AbstractValidator<ClaimMatchCommand>
    {
        public ClaimMatchCommandValidator()
        {
            RuleFor(x => x.TournamentId)
                .NotEmpty().WithMessage("TournamentId is required.");
            RuleFor(x => x.MatchId)
                .NotEmpty().WithMessage("MatchId is required.");
        }
    }
}

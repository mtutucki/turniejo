using FluentValidation;

namespace Turniejo.Matches.Application.Matches.ReleaseMatch
{
    public sealed class ReleaseMatchCommandValidator : AbstractValidator<ReleaseMatchCommand>
    {
        public ReleaseMatchCommandValidator()
        {
            RuleFor(x => x.TournamentId)
                .NotEmpty().WithMessage("TournamentId is required.");
            RuleFor(x => x.MatchId)
                .NotEmpty().WithMessage("MatchId is required.");
        }
    }
}

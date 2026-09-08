using FluentValidation;
using Turniejo.Matches.Domain.Enums;

namespace Turniejo.Matches.Application.Matches.FinishMatch
{
    public sealed class FinishMatchCommandValidator : AbstractValidator<FinishMatchCommand>
    {
        public FinishMatchCommandValidator()
        {
            RuleFor(x => x.TournamentId)
                .NotEmpty().WithMessage("TournamentId is required.");
            RuleFor(x => x.MatchId)
                .NotEmpty().WithMessage("MatchId is required.");
            RuleFor(x => x.ResultType)
                .IsInEnum().WithMessage("ResultType is invalid.")
                .NotEqual(MatchResultType.NotPlayed).WithMessage("ResultType must indicate how the match was decided.");
        }
    }
}

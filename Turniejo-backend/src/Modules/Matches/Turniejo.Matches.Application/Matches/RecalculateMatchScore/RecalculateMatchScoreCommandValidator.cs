using FluentValidation;

namespace Turniejo.Matches.Application.Matches.RecalculateMatchScore
{
    internal sealed class RecalculateMatchScoreCommandValidator : AbstractValidator<RecalculateMatchScoreCommand>
    {
        public RecalculateMatchScoreCommandValidator()
        {
            RuleFor(x => x.MatchId)
                .NotEmpty().WithMessage("MatchId is required.");
        }
    }
}

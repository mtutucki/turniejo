using FluentValidation;

namespace Turniejo.Matches.Application.Matches.CorrectFinishedMatchScore
{
    public sealed class CorrectFinishedMatchScoreCommandValidator : AbstractValidator<CorrectFinishedMatchScoreCommand>
    {
        public CorrectFinishedMatchScoreCommandValidator()
        {
            RuleFor(x => x.TournamentId)
                .NotEmpty().WithMessage("TournamentId is required.");
            RuleFor(x => x.MatchId)
                .NotEmpty().WithMessage("MatchId is required.");
            RuleFor(x => x.HomeScore)
                .GreaterThanOrEqualTo(0).WithMessage("HomeScore cannot be negative.");
            RuleFor(x => x.AwayScore)
                .GreaterThanOrEqualTo(0).WithMessage("AwayScore cannot be negative.");
        }
    }
}

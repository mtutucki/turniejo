using FluentValidation;

namespace Turniejo.Matches.Application.Matches.SetMatchScore
{
    public sealed class SetMatchScoreCommandValidator : AbstractValidator<SetMatchScoreCommand>
    {
        public SetMatchScoreCommandValidator()
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

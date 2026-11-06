using FluentValidation;
using Turniejo.Matches.Domain.Enums;

namespace Turniejo.Matches.Application.Matches.ChangeFinishedMatchResult
{
    public sealed class ChangeFinishedMatchResultCommandValidator : AbstractValidator<ChangeFinishedMatchResultCommand>
    {
        private const int MaxGoals = 99;

        public ChangeFinishedMatchResultCommandValidator()
        {
            RuleFor(x => x.TournamentId)
                .NotEmpty().WithMessage("TournamentId is required.");
            RuleFor(x => x.MatchId)
                .NotEmpty().WithMessage("MatchId is required.");
            // Te same sposoby rozstrzygnięcia, które sędzia wybiera przy kończeniu meczu.
            RuleFor(x => x.ResultType)
                .Must(t => t is MatchResultType.Regular or MatchResultType.ExtraTime or MatchResultType.Penalties or MatchResultType.Walkover)
                .WithMessage("ResultType must be Regular, ExtraTime, Penalties or Walkover.");
            RuleFor(x => x.HomeScore)
                .InclusiveBetween(0, MaxGoals).When(x => x.HomeScore.HasValue)
                .WithMessage("Score must be between 0 and 99.");
            RuleFor(x => x.AwayScore)
                .InclusiveBetween(0, MaxGoals).When(x => x.AwayScore.HasValue)
                .WithMessage("Score must be between 0 and 99.");
        }
    }
}

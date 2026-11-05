using FluentValidation;
using Turniejo.Matches.Domain.Enums;

namespace Turniejo.Matches.Application.Matches.RecordMatchResult
{
    public sealed class RecordMatchResultCommandValidator : AbstractValidator<RecordMatchResultCommand>
    {
        public RecordMatchResultCommandValidator()
        {
            RuleFor(x => x.TournamentId)
                .NotEmpty().WithMessage("TournamentId is required.");
            RuleFor(x => x.MatchId)
                .NotEmpty().WithMessage("MatchId is required.");
            RuleFor(x => x.HomeScore)
                .InclusiveBetween(0, RecordMatchResultCommand.MaxGoals).WithMessage("Score must be between 0 and 99.");
            RuleFor(x => x.AwayScore)
                .InclusiveBetween(0, RecordMatchResultCommand.MaxGoals).WithMessage("Score must be between 0 and 99.");
            RuleFor(x => x.ResultType)
                .Must(t => t is MatchResultType.Regular or MatchResultType.ExtraTime or MatchResultType.Penalties or MatchResultType.Walkover)
                .WithMessage("ResultType must be Regular, ExtraTime, Penalties or Walkover.");
        }
    }
}

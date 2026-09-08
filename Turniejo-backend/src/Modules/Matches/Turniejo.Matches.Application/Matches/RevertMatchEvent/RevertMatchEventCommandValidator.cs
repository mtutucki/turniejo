using FluentValidation;

namespace Turniejo.Matches.Application.Matches.RevertMatchEvent
{
    public sealed class RevertMatchEventCommandValidator : AbstractValidator<RevertMatchEventCommand>
    {
        public RevertMatchEventCommandValidator()
        {
            RuleFor(x => x.TournamentId)
                .NotEmpty().WithMessage("TournamentId is required.");
            RuleFor(x => x.MatchId)
                .NotEmpty().WithMessage("MatchId is required.");
            RuleFor(x => x.MatchEventId)
                .NotEmpty().WithMessage("MatchEventId is required.");
        }
    }
}

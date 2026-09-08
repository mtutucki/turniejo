using FluentValidation;

namespace Turniejo.Matches.Application.Matches.UpdateMatchEvent
{
    public sealed class UpdateMatchEventCommandValidator : AbstractValidator<UpdateMatchEventCommand>
    {
        public UpdateMatchEventCommandValidator()
        {
            RuleFor(x => x.TournamentId)
                .NotEmpty().WithMessage("TournamentId is required.");
            RuleFor(x => x.MatchId)
                .NotEmpty().WithMessage("MatchId is required.");
            RuleFor(x => x.MatchEventId)
                .NotEmpty().WithMessage("MatchEventId is required.");
            RuleFor(x => x.Minute)
                .GreaterThanOrEqualTo(0).WithMessage("Minute cannot be negative.");
            RuleFor(x => x.Second)
                .InclusiveBetween(0, 59).When(x => x.Second.HasValue).WithMessage("Second must be between 0 and 59.");
        }
    }
}

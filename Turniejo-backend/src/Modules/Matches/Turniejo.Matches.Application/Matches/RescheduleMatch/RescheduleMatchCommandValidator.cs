using FluentValidation;

namespace Turniejo.Matches.Application.Matches.RescheduleMatch
{
    public sealed class RescheduleMatchCommandValidator : AbstractValidator<RescheduleMatchCommand>
    {
        public RescheduleMatchCommandValidator()
        {
            RuleFor(x => x.TournamentId)
                .NotEmpty().WithMessage("TournamentId is required.");
            RuleFor(x => x.MatchId)
                .NotEmpty().WithMessage("MatchId is required.");
        }
    }
}

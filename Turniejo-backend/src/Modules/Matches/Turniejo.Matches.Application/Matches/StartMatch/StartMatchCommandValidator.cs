using FluentValidation;

namespace Turniejo.Matches.Application.Matches.StartMatch
{
    public sealed class StartMatchCommandValidator : AbstractValidator<StartMatchCommand>
    {
        public StartMatchCommandValidator()
        {
            RuleFor(x => x.TournamentId)
                .NotEmpty().WithMessage("TournamentId is required.");
            RuleFor(x => x.MatchId)
                .NotEmpty().WithMessage("MatchId is required.");
        }
    }
}

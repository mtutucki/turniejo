using FluentValidation;

namespace Turniejo.Matches.Application.Matches.AssignRefereeToMatch
{
    public sealed class AssignRefereeToMatchCommandValidator : AbstractValidator<AssignRefereeToMatchCommand>
    {
        public AssignRefereeToMatchCommandValidator()
        {
            RuleFor(x => x.TournamentId)
                .NotEmpty().WithMessage("TournamentId is required.");
            RuleFor(x => x.MatchId)
                .NotEmpty().WithMessage("MatchId is required.");
        }
    }
}

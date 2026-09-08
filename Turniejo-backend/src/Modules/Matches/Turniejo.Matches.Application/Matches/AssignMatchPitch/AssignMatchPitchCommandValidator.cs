using FluentValidation;

namespace Turniejo.Matches.Application.Matches.AssignMatchPitch
{
    public sealed class AssignMatchPitchCommandValidator : AbstractValidator<AssignMatchPitchCommand>
    {
        public AssignMatchPitchCommandValidator()
        {
            RuleFor(x => x.TournamentId)
                .NotEmpty().WithMessage("TournamentId is required.");
            RuleFor(x => x.MatchId)
                .NotEmpty().WithMessage("MatchId is required.");
        }
    }
}

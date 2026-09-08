using FluentValidation;

namespace Turniejo.Matches.Application.Matches.ResumeMatch
{
    public sealed class ResumeMatchCommandValidator : AbstractValidator<ResumeMatchCommand>
    {
        public ResumeMatchCommandValidator()
        {
            RuleFor(x => x.TournamentId)
                .NotEmpty().WithMessage("TournamentId is required.");
            RuleFor(x => x.MatchId)
                .NotEmpty().WithMessage("MatchId is required.");
        }
    }
}

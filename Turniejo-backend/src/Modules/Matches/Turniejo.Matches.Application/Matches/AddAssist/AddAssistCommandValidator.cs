using FluentValidation;

namespace Turniejo.Matches.Application.Matches.AddAssist
{
    public sealed class AddAssistCommandValidator : AbstractValidator<AddAssistCommand>
    {
        public AddAssistCommandValidator()
        {
            RuleFor(x => x.TournamentId)
                .NotEmpty().WithMessage("TournamentId is required.");
            RuleFor(x => x.MatchId)
                .NotEmpty().WithMessage("MatchId is required.");
            RuleFor(x => x.TournamentTeamId)
                .NotEmpty().WithMessage("TournamentTeamId is required.");
            RuleFor(x => x.PlayerId)
                .NotEmpty().WithMessage("PlayerId is required.");
            RuleFor(x => x.Minute)
                .InclusiveBetween(0, 300).WithMessage("Minute must be between 0 and 300.");
            RuleFor(x => x.Second)
                .InclusiveBetween(0, 59).When(x => x.Second.HasValue).WithMessage("Second must be between 0 and 59.");
        }
    }
}

using FluentValidation;

namespace Turniejo.Matches.Application.Matches.UpdateMatchSchedule
{
    public sealed class UpdateMatchScheduleCommandValidator : AbstractValidator<UpdateMatchScheduleCommand>
    {
        public UpdateMatchScheduleCommandValidator()
        {
            RuleFor(x => x.TournamentId)
                .NotEmpty().WithMessage("TournamentId is required.");
            RuleFor(x => x.MatchId)
                .NotEmpty().WithMessage("MatchId is required.");
        }
    }
}

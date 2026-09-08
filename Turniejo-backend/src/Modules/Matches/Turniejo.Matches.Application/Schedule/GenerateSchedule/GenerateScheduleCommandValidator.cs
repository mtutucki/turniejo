using FluentValidation;

namespace Turniejo.Matches.Application.Schedule.GenerateSchedule
{
    public sealed class GenerateScheduleCommandValidator : AbstractValidator<GenerateScheduleCommand>
    {
        public GenerateScheduleCommandValidator()
        {
            RuleFor(x => x.TournamentId)
                .NotEmpty().WithMessage("TournamentId is required.");
            RuleFor(x => x.PitchIds)
                .NotEmpty().WithMessage("At least one pitch is required.");
            RuleFor(x => x.EndAt)
                .GreaterThan(x => x.StartAt).WithMessage("EndAt must be after StartAt.");
        }
    }
}

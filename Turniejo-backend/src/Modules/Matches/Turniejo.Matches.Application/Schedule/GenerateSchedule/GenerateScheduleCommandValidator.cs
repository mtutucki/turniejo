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
            RuleForEach(x => x.Windows)
                .Must(w => w.EndAt > w.StartAt).WithMessage("Each day must end after it starts.");
            RuleFor(x => x.Windows)
                .Must(windows =>
                {
                    var ordered = windows!.OrderBy(w => w.StartAt).ToList();
                    return ordered.Zip(ordered.Skip(1)).All(pair => pair.First.EndAt <= pair.Second.StartAt);
                })
                .When(x => x.Windows is { Count: > 1 })
                .WithMessage("Days must not overlap.");
        }
    }
}

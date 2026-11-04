using FluentValidation;

namespace Turniejo.Matches.Application.Matches.UpdatePlannedMatchSchedule
{
    public sealed class UpdatePlannedMatchScheduleCommandValidator : AbstractValidator<UpdatePlannedMatchScheduleCommand>
    {
        public UpdatePlannedMatchScheduleCommandValidator()
        {
            RuleFor(x => x.TournamentId).NotEmpty().WithMessage("TournamentId is required.");
            RuleFor(x => x.NodeId).NotEmpty().WithMessage("NodeId is required.");
            RuleFor(x => x.PitchId).NotEmpty().WithMessage("PitchId is required.");
        }
    }
}

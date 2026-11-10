using FluentValidation;

namespace Turniejo.Brackets.Application.SecondStage.SetSecondStageSeats
{
    public sealed class SetSecondStageSeatsCommandValidator : AbstractValidator<SetSecondStageSeatsCommand>
    {
        public SetSecondStageSeatsCommandValidator()
        {
            RuleFor(x => x.CategoryId).NotEmpty().WithMessage("CategoryId is required");
            RuleFor(x => x.Seats).NotNull().WithMessage("Seats are required");
            RuleForEach(x => x.Seats).ChildRules(seat =>
            {
                seat.RuleFor(s => s.GroupId).NotEmpty().WithMessage("GroupId is required");
                seat.RuleFor(s => s.SourceGroupId).NotEmpty().WithMessage("SourceGroupId is required");
                seat.RuleFor(s => s.SourcePosition).GreaterThanOrEqualTo(1).WithMessage("Place in the group must be at least 1");
            });
        }
    }
}

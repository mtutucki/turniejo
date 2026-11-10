using FluentValidation;

namespace Turniejo.Brackets.Application.SecondStage.SetSecondStageRanges
{
    public sealed class SetSecondStageRangesCommandValidator : AbstractValidator<SetSecondStageRangesCommand>
    {
        public const int MaxPosition = 64;

        public SetSecondStageRangesCommandValidator()
        {
            RuleFor(x => x.CategoryId).NotEmpty().WithMessage("CategoryId is required");
            RuleFor(x => x.Ranges).NotNull().WithMessage("Ranges are required");
            RuleForEach(x => x.Ranges).ChildRules(range =>
            {
                range.RuleFor(r => r.GroupId).NotEmpty().WithMessage("GroupId is required");
                range.RuleFor(r => r.FromPosition).InclusiveBetween(1, MaxPosition).WithMessage("Place in the group must be at least 1");
                range.RuleFor(r => r.ToPosition).InclusiveBetween(1, MaxPosition).WithMessage("Place in the group must be at least 1");
            });
        }
    }
}

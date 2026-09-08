using FluentValidation;

namespace Turniejo.Players.Application.PlayerGuardians.AssignGuardianToPlayer
{
    public sealed class AssignGuardianToPlayerCommandValidator : AbstractValidator<AssignGuardianToPlayerCommand>
    {
        public AssignGuardianToPlayerCommandValidator()
        {
            RuleFor(x => x.PlayerId)
                .NotEmpty().WithMessage("PlayerId is required.");
            RuleFor(x => x.UserId)
                .NotEmpty().WithMessage("UserId is required.");
            RuleFor(x => x.RelationshipType)
                .IsInEnum().WithMessage("RelationshipType is not a valid value.");
        }
    }
}

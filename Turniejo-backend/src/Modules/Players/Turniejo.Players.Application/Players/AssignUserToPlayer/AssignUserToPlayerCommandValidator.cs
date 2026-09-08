using FluentValidation;

namespace Turniejo.Players.Application.Players.AssignUserToPlayer
{
    public sealed class AssignUserToPlayerCommandValidator : AbstractValidator<AssignUserToPlayerCommand>
    {
        public AssignUserToPlayerCommandValidator()
        {
            RuleFor(x => x.PlayerId)
                .NotEmpty().WithMessage("PlayerId is required.");
            RuleFor(x => x.UserId)
                .NotEmpty().WithMessage("UserId is required.");
        }
    }
}

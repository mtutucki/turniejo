using FluentValidation;

namespace Turniejo.Players.Application.TeamPlayers.CreateTeamPlayer
{
    public sealed class CreateTeamPlayerCommandValidator : AbstractValidator<CreateTeamPlayerCommand>
    {
        public CreateTeamPlayerCommandValidator()
        {
            RuleFor(x => x.TeamId)
                .NotEmpty().WithMessage("TeamId is required.");
            RuleFor(x => x.FirstName)
                .NotEmpty().WithMessage("FirstName is required.")
                .MaximumLength(100).WithMessage("FirstName must not exceed 100 characters.");
            RuleFor(x => x.LastName)
                .NotEmpty().WithMessage("LastName is required.")
                .MaximumLength(100).WithMessage("LastName must not exceed 100 characters.");
            RuleFor(x => x.BirthDate)
                .LessThanOrEqualTo(DateTime.UtcNow.Date).WithMessage("BirthDate cannot be in the future.");
            RuleFor(x => x.ShirtNumber)
                .GreaterThan(0).When(x => x.ShirtNumber.HasValue).WithMessage("ShirtNumber must be greater than 0.");
        }
    }
}

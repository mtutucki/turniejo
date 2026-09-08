using FluentValidation;

namespace Turniejo.Tournaments.Application.Tournaments.CreateTournament
{
    public sealed class CreateTournamentCommandValidator : AbstractValidator<CreateTournamentCommand>
    {
        public CreateTournamentCommandValidator()
        {
            RuleFor(x => x.OrganizationId)
                .NotEmpty().WithMessage("OrganizationId is required.");

            RuleFor(x => x.Name)
                .NotEmpty().WithMessage("Name is required.")
                .MaximumLength(200).WithMessage("Name must not exceed 200 characters.");

            RuleFor(x => x.StartAt)
                .NotEmpty().WithMessage("StartAt is required.")
                .NotEqual(DateTimeOffset.MinValue).WithMessage("StartAt is required.");

            RuleFor(x => x.EndAt)
                .NotEmpty().WithMessage("EndAt is required.")
                .GreaterThan(x => x.StartAt).WithMessage("EndAt must be after StartAt.");

            RuleFor(x => x.TeamLimit)
                .GreaterThanOrEqualTo(0).WithMessage("TeamLimit must be greater than or equal to 0.");

            RuleFor(x => x.Description)
                .MaximumLength(500).WithMessage("Description must not exceed 500 characters.");
        }
    }
}

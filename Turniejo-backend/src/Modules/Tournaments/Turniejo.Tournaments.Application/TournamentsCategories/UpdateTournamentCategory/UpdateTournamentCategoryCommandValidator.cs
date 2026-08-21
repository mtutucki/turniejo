using FluentValidation;

namespace Turniejo.Tournaments.Application.TournamentsCategories.UpdateTournamentCategory
{
    public sealed class UpdateTournamentCategoryCommandValidator : AbstractValidator<UpdateTournamentCategoryCommand>
    {
        public UpdateTournamentCategoryCommandValidator()
        {
            RuleFor(x => x.Name)
                .NotEmpty().WithMessage("Name is required.")
                .MaximumLength(100).WithMessage("Name must not exceed 100 characters.");
            RuleFor(x => x.MinBirthDate)
                .LessThanOrEqualTo(x => x.MaxBirthDate).When(x => x.MinBirthDate.HasValue && x.MaxBirthDate.HasValue)
                .WithMessage("MinBirthDate must be less than or equal to MaxBirthDate.");
        }
    }
}

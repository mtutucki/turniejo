using FluentValidation;
using Turniejo.Tournaments.Application.Services;

namespace Turniejo.Tournaments.Application.TournamentsCategories.AddTournamentCategory
{
    public sealed class AddTournamentCategoryCommandValidator : AbstractValidator<AddTournamentCategoryCommand>
    {
        public AddTournamentCategoryCommandValidator()
        {
            RuleFor(x => x.TournamentId)
                .NotEmpty().WithMessage("TournamentId is required.");
            RuleFor(x => x.Name)
                .NotEmpty().WithMessage("Name is required.")
                .MaximumLength(100).WithMessage("Name must not exceed 100 characters.");
            RuleFor(x => x.MinBirthDate)
                .LessThanOrEqualTo(x => x.MaxBirthDate).When(x => x.MinBirthDate.HasValue && x.MaxBirthDate.HasValue)
                .WithMessage("MinBirthDate must be less than or equal to MaxBirthDate.");
            RuleFor(x => x.GroupCount)
                .InclusiveBetween(1, DefaultGroupProvisioner.MaxGroupsOnCreate)
                .WithMessage($"GroupCount must be between 1 and {DefaultGroupProvisioner.MaxGroupsOnCreate}.");
        }
    }
}

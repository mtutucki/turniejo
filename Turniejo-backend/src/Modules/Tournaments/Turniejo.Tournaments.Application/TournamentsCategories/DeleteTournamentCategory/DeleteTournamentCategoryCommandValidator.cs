using FluentValidation;

namespace Turniejo.Tournaments.Application.TournamentsCategories.DeleteTournamentCategory
{
    public sealed class DeleteTournamentCategoryCommandValidator : AbstractValidator<DeleteTournamentCategoryCommand>
    {
        public DeleteTournamentCategoryCommandValidator()
        {
            RuleFor(x => x.TournamentId)
                .NotEmpty().WithMessage("TournamentId is required.");

            RuleFor(x => x.CategoryId)
                .NotEmpty().WithMessage("CategoryId is required.");
        }
    }
}

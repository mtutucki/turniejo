using FluentValidation;

namespace Turniejo.Tournaments.Application.TournamentGroups.AddTournamentGroup
{
    public sealed class AddTournamentGroupCommandValidator : AbstractValidator<AddTournamentGroupCommand>
    {
        public AddTournamentGroupCommandValidator()
        {
            RuleFor(x => x.TournamentId)
                .NotEmpty()
                .WithMessage("TournamentId is required");

            RuleFor(x => x.CategoryId)
                .NotEmpty()
                .WithMessage("CategoryId is required");

            RuleFor(x => x.Name)
                .NotEmpty().WithMessage("Name is required.")
                .MaximumLength(100).WithMessage("Name must not exceed 100 characters.");
        }
    }
}

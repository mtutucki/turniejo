using FluentValidation;

namespace Turniejo.Tournaments.Application.TournamentGroups.UpdateTournamentGroup
{
    public sealed class UpdateTournamentGroupCommandValidator : AbstractValidator<UpdateTournamentGroupCommand>
    {
        public UpdateTournamentGroupCommandValidator()
        {
            RuleFor(x => x.TournamentId)
                .NotEmpty().WithMessage("TournamentId is required.");
            RuleFor(x => x.GroupId)
                .NotEmpty().WithMessage("GroupId is required.");
            RuleFor(x => x.Name)
                .NotEmpty().WithMessage("Name is required.")
                .MaximumLength(100).WithMessage("Name must not exceed 100 characters.");
        }
    }
}

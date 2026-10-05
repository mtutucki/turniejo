using FluentValidation;

namespace Turniejo.Tournaments.Application.TournamentGroups.DeleteTournamentGroup
{
    public sealed class DeleteTournamentGroupCommandValidator : AbstractValidator<DeleteTournamentGroupCommand>
    {
        public DeleteTournamentGroupCommandValidator()
        {
            RuleFor(x => x.TournamentId)
                .NotEmpty().WithMessage("TournamentId is required.");
            RuleFor(x => x.GroupId)
                .NotEmpty().WithMessage("GroupId is required.");
        }
    }
}

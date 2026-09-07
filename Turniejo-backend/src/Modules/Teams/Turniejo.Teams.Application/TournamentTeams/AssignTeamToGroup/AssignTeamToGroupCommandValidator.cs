using FluentValidation;

namespace Turniejo.Teams.Application.TournamentTeams.AssignTeamToGroup
{
    public sealed class AssignTeamToGroupCommandValidator : AbstractValidator<AssignTeamToGroupCommand>
    {
        public AssignTeamToGroupCommandValidator()
        {
            RuleFor(x => x.TournamentId)
                .NotEmpty().WithMessage("TournamentId is required.");
            RuleFor(x => x.TournamentTeamId)
                .NotEmpty().WithMessage("TournamentTeamId is required.");
        }
    }
}

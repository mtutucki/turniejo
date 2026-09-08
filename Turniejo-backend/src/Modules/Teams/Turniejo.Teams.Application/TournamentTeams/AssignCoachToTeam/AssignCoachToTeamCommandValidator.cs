using FluentValidation;

namespace Turniejo.Teams.Application.TournamentTeams.AssignCoachToTeam
{
    public sealed class AssignCoachToTeamCommandValidator : AbstractValidator<AssignCoachToTeamCommand>
    {
        public AssignCoachToTeamCommandValidator()
        {
            RuleFor(x => x.TournamentId)
                .NotEmpty().WithMessage("TournamentId is required.");
            RuleFor(x => x.TournamentTeamId)
                .NotEmpty().WithMessage("TournamentTeamId is required.");
            RuleFor(x => x.UserId)
                .NotEmpty().WithMessage("UserId is required.");
        }
    }
}

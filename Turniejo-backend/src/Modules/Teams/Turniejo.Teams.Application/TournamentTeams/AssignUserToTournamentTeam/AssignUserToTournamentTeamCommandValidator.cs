using FluentValidation;

namespace Turniejo.Teams.Application.TournamentTeams.AssignUserToTournamentTeam
{
    public sealed class AssignUserToTournamentTeamCommandValidator : AbstractValidator<AssignUserToTournamentTeamCommand>
    {
        public AssignUserToTournamentTeamCommandValidator()
        {
            RuleFor(x => x.TournamentId)
                .NotEmpty().WithMessage("TournamentId is required.");
            RuleFor(x => x.TournamentTeamId)
                .NotEmpty().WithMessage("TournamentTeamId is required.");
            RuleFor(x => x.UserId)
                .NotEmpty().WithMessage("UserId is required.");
            RuleFor(x => x.Role)
                .IsInEnum().WithMessage("Role is not a valid team staff role.");
        }
    }
}

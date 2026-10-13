using FluentValidation;

namespace Turniejo.Teams.Application.TournamentTeams.RemoveStaffFromTeam
{
    public sealed class RemoveStaffFromTeamCommandValidator : AbstractValidator<RemoveStaffFromTeamCommand>
    {
        public RemoveStaffFromTeamCommandValidator()
        {
            RuleFor(x => x.TournamentId)
                .NotEmpty().WithMessage("TournamentId is required.");
            RuleFor(x => x.TournamentTeamId)
                .NotEmpty().WithMessage("TournamentTeamId is required.");
            RuleFor(x => x.TeamStaffId)
                .NotEmpty().WithMessage("TeamStaffId is required.");
        }
    }
}

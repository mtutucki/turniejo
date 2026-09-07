using FluentValidation;

namespace Turniejo.Teams.Application.TournamentTeams.AddTeamToTournament
{
    public sealed class AddTeamToTournamentCommandValidator : AbstractValidator<AddTeamToTournamentCommand>
    {
        public AddTeamToTournamentCommandValidator()
        {
            RuleFor(x => x.TournamentId)
                .NotEmpty().WithMessage("TournamentId is required.");
            RuleFor(x => x.TeamId)
                .NotEmpty().WithMessage("TeamId is required.");
            RuleFor(x => x.CategoryId)
                .NotEmpty().WithMessage("CategoryId is required.");
        }
    }
}

using FluentValidation;

namespace Turniejo.Players.Application.TournamentPlayers.AssignPlayerToTournamentTeam
{
    public sealed class AssignPlayerToTournamentTeamCommandValidator : AbstractValidator<AssignPlayerToTournamentTeamCommand>
    {
        public AssignPlayerToTournamentTeamCommandValidator()
        {
            RuleFor(x => x.TournamentId)
                .NotEmpty().WithMessage("TournamentId is required.");
            RuleFor(x => x.TournamentTeamId)
                .NotEmpty().WithMessage("TournamentTeamId is required.");
            RuleFor(x => x.PlayerId)
                .NotEmpty().WithMessage("PlayerId is required.");
            RuleFor(x => x.ShirtNumber)
                .GreaterThan(0).When(x => x.ShirtNumber.HasValue).WithMessage("ShirtNumber must be greater than 0.");
        }
    }
}

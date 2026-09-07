using FluentValidation;

namespace Turniejo.Teams.Application.TournamentTeams.ChangeTournamentTeamRegistrationStatus
{
    public sealed class ChangeTournamentTeamRegistrationStatusCommandValidator : AbstractValidator<ChangeTournamentTeamRegistrationStatusCommand>
    {
        public ChangeTournamentTeamRegistrationStatusCommandValidator()
        {
            RuleFor(x => x.TournamentId)
                .NotEmpty().WithMessage("TournamentId is required.");
            RuleFor(x => x.TournamentTeamId)
                .NotEmpty().WithMessage("TournamentTeamId is required.");
            RuleFor(x => x.NewStatus)
                .IsInEnum().WithMessage("NewStatus is not a valid registration status.");
        }
    }
}

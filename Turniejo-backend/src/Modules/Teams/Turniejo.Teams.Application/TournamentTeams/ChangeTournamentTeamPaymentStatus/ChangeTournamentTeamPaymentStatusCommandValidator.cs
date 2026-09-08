using FluentValidation;

namespace Turniejo.Teams.Application.TournamentTeams.ChangeTournamentTeamPaymentStatus
{
    public sealed class ChangeTournamentTeamPaymentStatusCommandValidator : AbstractValidator<ChangeTournamentTeamPaymentStatusCommand>
    {
        public ChangeTournamentTeamPaymentStatusCommandValidator()
        {
            RuleFor(x => x.TournamentId)
                .NotEmpty().WithMessage("TournamentId is required.");
            RuleFor(x => x.TournamentTeamId)
                .NotEmpty().WithMessage("TournamentTeamId is required.");
            RuleFor(x => x.NewStatus)
                .IsInEnum().WithMessage("NewStatus is not a valid payment status.");
        }
    }
}

using FluentValidation;

namespace Turniejo.Players.Application.TournamentPlayers.UpdateTournamentPlayer
{
    public sealed class UpdateTournamentPlayerCommandValidator : AbstractValidator<UpdateTournamentPlayerCommand>
    {
        public UpdateTournamentPlayerCommandValidator()
        {
            RuleFor(x => x.TournamentId)
                .NotEmpty().WithMessage("TournamentId is required.");
            RuleFor(x => x.TournamentTeamId)
                .NotEmpty().WithMessage("TournamentTeamId is required.");
            RuleFor(x => x.TournamentPlayerId)
                .NotEmpty().WithMessage("TournamentPlayerId is required.");
            RuleFor(x => x.ShirtNumber)
                .GreaterThan(0).When(x => x.ShirtNumber.HasValue).WithMessage("ShirtNumber must be greater than 0.");
            RuleFor(x => x.Status)
                .IsInEnum().WithMessage("Status is not a valid tournament player status.");
        }
    }
}

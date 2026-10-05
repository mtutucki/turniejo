using FluentValidation;

namespace Turniejo.Tournaments.Application.Tournaments.UpdateTournamentPublicSettings
{
    public sealed class UpdateTournamentPublicSettingsCommandValidator : AbstractValidator<UpdateTournamentPublicSettingsCommand>
    {
        public UpdateTournamentPublicSettingsCommandValidator()
        {
            RuleFor(x => x.TournamentId)
                .NotEmpty().WithMessage("TournamentId is required.");
        }
    }
}

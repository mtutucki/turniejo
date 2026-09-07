using FluentValidation;

namespace Turniejo.Tournaments.Application.Tournaments.TournamentSetting.UpdateTournamentSettings
{
    public sealed class UpdateTournamentSettingsCommandValidator : AbstractValidator<UpdateTournamentSettingsCommand>
    {
        public UpdateTournamentSettingsCommandValidator()
        {
            RuleFor(x => x.TournamentId)
                .NotEmpty().WithMessage("TournamentId is required.");

            RuleFor(x => x.PointsForLoss)
                .GreaterThanOrEqualTo(0).WithMessage("PointsForLoss cannot be negative.");

            RuleFor(x => x)
                .Must(x => x.PointsForDraw >= x.PointsForLoss)
                .WithName("PointsForDraw")
                .WithMessage("PointsForDraw cannot be less than PointsForLoss.");

            RuleFor(x => x)
                .Must(x => x.PointsForWin >= x.PointsForDraw)
                .WithName("PointsForWin")
                .WithMessage("PointsForWin cannot be less than PointsForDraw.");

            RuleFor(x => x.MatchDurationMinutes)
                .GreaterThan(0).WithMessage("MatchDurationMinutes must be higher than 0.");

            RuleFor(x => x.BreakDurationMinutes)
                .GreaterThanOrEqualTo(0).WithMessage("BreakDurationMinutes cannot be negative.");

            RuleFor(x => x.RefereeEditWindowMinutes)
                .GreaterThanOrEqualTo(0).WithMessage("RefereeEditWindowMinutes cannot be negative.");

            RuleFor(x => x)
                .Must(x => x.WalkoverHomeScore != x.WalkoverAwayScore)
                .WithName("WalkoverHomeScore")
                .WithMessage("WalkoverHomeScore cannot equal WalkoverAwayScore.");
        }
    }
}
using FluentValidation;
using Turniejo.Matches.Domain.Enums;

namespace Turniejo.Matches.Application.Matches.AddGoal
{
    public sealed class AddGoalCommandValidator : AbstractValidator<AddGoalCommand>
    {
        public AddGoalCommandValidator()
        {
            RuleFor(x => x.TournamentId)
                .NotEmpty().WithMessage("TournamentId is required.");
            RuleFor(x => x.MatchId)
                .NotEmpty().WithMessage("MatchId is required.");
            RuleFor(x => x.TournamentTeamId)
                .NotEmpty().WithMessage("TournamentTeamId is required.");
            RuleFor(x => x.GoalType)
                .Must(t => t is MatchEventType.Goal or MatchEventType.OwnGoal or MatchEventType.PenaltyGoal)
                .WithMessage("GoalType must be Goal, OwnGoal or PenaltyGoal.");
            RuleFor(x => x.Minute)
                .GreaterThanOrEqualTo(0).WithMessage("Minute cannot be negative.");
            RuleFor(x => x.Second)
                .InclusiveBetween(0, 59).When(x => x.Second.HasValue).WithMessage("Second must be between 0 and 59.");
        }
    }
}

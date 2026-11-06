using FluentValidation;

namespace Turniejo.Matches.Application.Matches.SetGoalPlayers
{
    public sealed class SetGoalPlayersCommandValidator : AbstractValidator<SetGoalPlayersCommand>
    {
        public SetGoalPlayersCommandValidator()
        {
            RuleFor(x => x.TournamentId).NotEmpty();
            RuleFor(x => x.MatchId).NotEmpty();
            RuleFor(x => x.MatchEventId).NotEmpty();
            RuleFor(x => x.AssistPlayerId)
                .NotEqual(x => x.PlayerId).When(x => x.AssistPlayerId.HasValue && x.PlayerId.HasValue)
                .WithMessage("Scorer cannot assist their own goal");
        }
    }
}

using FluentValidation;
using Turniejo.Matches.Domain.Enums;

namespace Turniejo.Matches.Application.Matches.AddCard
{
    public sealed class AddCardCommandValidator : AbstractValidator<AddCardCommand>
    {
        public AddCardCommandValidator()
        {
            RuleFor(x => x.TournamentId)
                .NotEmpty().WithMessage("TournamentId is required.");
            RuleFor(x => x.MatchId)
                .NotEmpty().WithMessage("MatchId is required.");
            RuleFor(x => x.TournamentTeamId)
                .NotEmpty().WithMessage("TournamentTeamId is required.");
            RuleFor(x => x.PlayerId)
                .NotEmpty().WithMessage("PlayerId is required.");
            RuleFor(x => x.CardType)
                .Must(t => t is MatchEventType.YellowCard or MatchEventType.RedCard)
                .WithMessage("CardType must be YellowCard or RedCard.");
            RuleFor(x => x.Minute)
                .GreaterThanOrEqualTo(0).WithMessage("Minute cannot be negative.");
            RuleFor(x => x.Second)
                .InclusiveBetween(0, 59).When(x => x.Second.HasValue).WithMessage("Second must be between 0 and 59.");
        }
    }
}

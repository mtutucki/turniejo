using FluentValidation;
using Turniejo.Brackets.Domain.Enums;

namespace Turniejo.Brackets.Application.Brackets.GenerateBracket
{
    public sealed class GenerateBracketCommandValidator : AbstractValidator<GenerateBracketCommand>
    {
        public GenerateBracketCommandValidator()
        {
            RuleFor(x => x.TournamentId)
                .NotEmpty().WithMessage("TournamentId is required.");
            RuleFor(x => x.CategoryId)
                .NotEmpty().WithMessage("CategoryId is required.");
            RuleFor(x => x.RoundOnePairings)
                .NotEmpty().WithMessage("At least one round-1 pairing is required.")
                .Must(pairings => IsPowerOfTwo(pairings.Count)).WithMessage("The number of round-1 pairings must be a power of two.");

            RuleForEach(x => x.RoundOnePairings).ChildRules(pairing =>
            {
                pairing.RuleFor(p => p.HomeTournamentTeamId)
                    .NotNull().When(p => p.HomeSourceType == BracketSourceType.Fixed)
                    .WithMessage("HomeTournamentTeamId is required for Fixed source.");
                pairing.RuleFor(p => p.HomeGroupId)
                    .NotNull().When(p => p.HomeSourceType == BracketSourceType.GroupPosition)
                    .WithMessage("HomeGroupId is required for GroupPosition source.");
                pairing.RuleFor(p => p.HomePosition)
                    .NotNull().When(p => p.HomeSourceType == BracketSourceType.GroupPosition)
                    .WithMessage("HomePosition is required for GroupPosition source.");
                pairing.RuleFor(p => p.AwayTournamentTeamId)
                    .NotNull().When(p => p.AwaySourceType == BracketSourceType.Fixed)
                    .WithMessage("AwayTournamentTeamId is required for Fixed source.");
                pairing.RuleFor(p => p.AwayGroupId)
                    .NotNull().When(p => p.AwaySourceType == BracketSourceType.GroupPosition)
                    .WithMessage("AwayGroupId is required for GroupPosition source.");
                pairing.RuleFor(p => p.AwayPosition)
                    .NotNull().When(p => p.AwaySourceType == BracketSourceType.GroupPosition)
                    .WithMessage("AwayPosition is required for GroupPosition source.");
            });
        }

        private static bool IsPowerOfTwo(int value) => value > 0 && (value & (value - 1)) == 0;
    }
}

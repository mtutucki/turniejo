using FluentValidation;

namespace Turniejo.Tournaments.Application.TieBreakerRules.SetTieBreakerRules
{
    public sealed class SetTieBreakerRulesCommandValidator : AbstractValidator<SetTieBreakerRulesCommand>
    {
        public SetTieBreakerRulesCommandValidator()
        {
            RuleFor(x => x.TournamentId)
                .NotEmpty().WithMessage("TournamentId is required.");

            RuleFor(x => x.Rules)
                .NotEmpty().WithMessage("Rules are required.");

            RuleForEach(x => x.Rules).ChildRules(rule =>
            {
                rule.RuleFor(r => r.Order)
                    .GreaterThanOrEqualTo(0).WithMessage("Order cannot be negative.");
            });
        }
    }
}
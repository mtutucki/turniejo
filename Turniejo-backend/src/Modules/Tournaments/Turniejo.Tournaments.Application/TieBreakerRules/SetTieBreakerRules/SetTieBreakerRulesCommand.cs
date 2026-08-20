using Turniejo.SharedKernel.CQRS;
using Turniejo.Tournaments.Domain.Enums;

namespace Turniejo.Tournaments.Application.TieBreakerRules.SetTieBreakerRules
{
    public sealed record TieBreakerRuleItem(int Order, TieBreakerType Type);

    public sealed record SetTieBreakerRulesCommand(
        Guid TournamentId,
        IReadOnlyList<TieBreakerRuleItem> Rules) : ICommand<SetTieBreakerRulesResponse>;
}
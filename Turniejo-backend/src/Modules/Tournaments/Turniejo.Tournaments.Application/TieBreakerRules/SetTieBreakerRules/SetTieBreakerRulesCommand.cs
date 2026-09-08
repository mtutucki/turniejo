using Turniejo.SharedKernel.CQRS;
using Turniejo.SharedKernel.Enums;
using Turniejo.Tournaments.Application.Authorization;
using Turniejo.Tournaments.Domain.Enums;

namespace Turniejo.Tournaments.Application.TieBreakerRules.SetTieBreakerRules
{
    public sealed record TieBreakerRuleItem(int Order, TieBreakerType Type);

    public sealed record SetTieBreakerRulesCommand(
        Guid TournamentId,
        IReadOnlyList<TieBreakerRuleItem> Rules) : ICommand<SetTieBreakerRulesResponse>, IAuthorizedRequest
    {
        public string ScopeType => nameof(RoleScopeType.Tournament);
        public Guid ScopeId => TournamentId;
        public IReadOnlyCollection<string> AllowedRoles => TournamentRoles.OrganizerOnly;
    }
}
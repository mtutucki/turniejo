using Turniejo.Brackets.Application.Authorization;
using Turniejo.Brackets.Domain.Enums;
using Turniejo.SharedKernel.CQRS;
using Turniejo.SharedKernel.Enums;

namespace Turniejo.Brackets.Application.Brackets.SwapBracketSlots
{
    // Ręczna poprawka losowania: organizator zamienia miejscami dwa sloty pierwszej rundy (drużynę,
    // miejsce w grupie albo wolny los), zamiast kasować całą drabinkę i układać pary od nowa. Struktura
    // drabinki, terminy i boiska zostają. Tylko dopóki żaden mecz drabinki się nie zaczął — potem
    // zamiana wymagałaby cofania awansów i wyników.
    public sealed record SwapBracketSlotsCommand(
        Guid TournamentId, Guid CategoryId,
        Guid FirstNodeId, BracketSlot FirstSlot,
        Guid SecondNodeId, BracketSlot SecondSlot) : ICommand, IAuthorizedRequest
    {
        public string ScopeType => nameof(RoleScopeType.Tournament);
        public Guid ScopeId => TournamentId;
        public IReadOnlyCollection<string> AllowedRoles => BracketRoles.OrganizerOnly;
    }
}

using Turniejo.Brackets.Application.Authorization;
using Turniejo.SharedKernel.CQRS;
using Turniejo.SharedKernel.Enums;

namespace Turniejo.Brackets.Application.Brackets.RecalculateBracket
{
    // Ręczna naprawa po pomyłce w wyniku: organizator przelicza drabinkę od pierwszej rundy, a zwycięzca
    // każdego zakończonego meczu trafia tam, gdzie powinien. Mecz kolejnej rundy, który już się zaczął,
    // zostaje bez zmian i wraca w BlockedMatchIds.
    public sealed record RecalculateBracketCommand(Guid TournamentId, Guid CategoryId) : ICommand<RecalculateBracketResponse>, IAuthorizedRequest
    {
        public string ScopeType => nameof(RoleScopeType.Tournament);
        public Guid ScopeId => TournamentId;
        public IReadOnlyCollection<string> AllowedRoles => BracketRoles.OrganizerOnly;
    }

    public sealed record RecalculateBracketResponse(bool Changed, IReadOnlyList<Guid> BlockedMatchIds);
}

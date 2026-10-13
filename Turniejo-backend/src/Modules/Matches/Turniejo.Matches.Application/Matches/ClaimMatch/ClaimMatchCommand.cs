using Turniejo.Matches.Application.Authorization;
using Turniejo.SharedKernel.CQRS;
using Turniejo.SharedKernel.Enums;

namespace Turniejo.Matches.Application.Matches.ClaimMatch
{
    // Mecz bez sędziego może wziąć każdy sędzia turnieju — stąd scope turnieju, a nie meczu
    // (roli na meczu jeszcze nie ma, dopiero przejęcie ją nadaje).
    public sealed record ClaimMatchCommand(Guid TournamentId, Guid MatchId)
        : ICommand<ClaimMatchResponse>, IAuthorizedRequest
    {
        public string ScopeType => nameof(RoleScopeType.Tournament);
        public Guid ScopeId => TournamentId;
        public IReadOnlyCollection<string> AllowedRoles => MatchRoles.RefereeOnly;
    }
}

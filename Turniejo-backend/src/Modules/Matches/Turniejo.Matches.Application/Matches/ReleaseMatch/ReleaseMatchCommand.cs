using Turniejo.Matches.Application.Authorization;
using Turniejo.SharedKernel.CQRS;
using Turniejo.SharedKernel.Enums;

namespace Turniejo.Matches.Application.Matches.ReleaseMatch
{
    // Oddać mecz może tylko sędzia, który go prowadzi; organizator zdejmuje sędziego przez
    // AssignRefereeToMatchCommand.
    public sealed record ReleaseMatchCommand(Guid TournamentId, Guid MatchId)
        : ICommand<ReleaseMatchResponse>, IAuthorizedRequest
    {
        public string ScopeType => nameof(RoleScopeType.Match);
        public Guid ScopeId => MatchId;
        public IReadOnlyCollection<string> AllowedRoles => MatchRoles.RefereeOnly;
    }
}

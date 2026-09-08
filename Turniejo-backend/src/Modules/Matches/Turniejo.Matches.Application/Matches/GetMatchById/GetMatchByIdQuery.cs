using Turniejo.Matches.Application.Authorization;
using Turniejo.Matches.Application.DTOs;
using Turniejo.SharedKernel.CQRS;
using Turniejo.SharedKernel.Enums;

namespace Turniejo.Matches.Application.Matches.GetMatchById
{
    public sealed record GetMatchByIdQuery(Guid TournamentId, Guid MatchId) : IQuery<MatchDto>, IAuthorizedRequest, IAlternateAuthorizedScope
    {
        // Primary: dowolny członek turnieju (Organizer/Referee/Coach/Viewer z rolą na poziomie turnieju).
        public string ScopeType => nameof(RoleScopeType.Tournament);
        public Guid ScopeId => TournamentId;
        public IReadOnlyCollection<string> AllowedRoles => MatchRoles.AnyTournamentMember;

        // Alternate: Policy Referee — sędzia przypisany do TEGO konkretnego meczu.
        public string AlternateScopeType => nameof(RoleScopeType.Match);
        public Guid AlternateScopeId => MatchId;
        public IReadOnlyCollection<string> AlternateAllowedRoles => MatchRoles.RefereeOnly;
    }
}

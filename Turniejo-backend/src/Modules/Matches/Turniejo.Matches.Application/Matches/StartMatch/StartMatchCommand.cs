using Turniejo.Matches.Application.Authorization;
using Turniejo.SharedKernel.CQRS;
using Turniejo.SharedKernel.Enums;

namespace Turniejo.Matches.Application.Matches.StartMatch
{
    public sealed record StartMatchCommand(Guid TournamentId, Guid MatchId)
        : ICommand<StartMatchResponse>, IAuthorizedRequest, IAlternateAuthorizedScope
    {
        // Primary: organizator całego turnieju.
        public string ScopeType => nameof(RoleScopeType.Tournament);
        public Guid ScopeId => TournamentId;
        public IReadOnlyCollection<string> AllowedRoles => MatchRoles.OrganizerOnly;

        // Alternate: Policy Referee — sędzia przypisany do TEGO konkretnego meczu.
        public string AlternateScopeType => nameof(RoleScopeType.Match);
        public Guid AlternateScopeId => MatchId;
        public IReadOnlyCollection<string> AlternateAllowedRoles => MatchRoles.RefereeOnly;
    }
}

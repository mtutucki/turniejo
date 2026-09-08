using Turniejo.Matches.Application.Authorization;
using Turniejo.Matches.Application.DTOs;
using Turniejo.SharedKernel.CQRS;
using Turniejo.SharedKernel.Enums;

namespace Turniejo.Matches.Application.Matches.GetMatchTimeline
{
    public sealed record GetMatchTimelineQuery(Guid TournamentId, Guid MatchId)
        : IQuery<IReadOnlyList<MatchTimelineEntryDto>>, IAuthorizedRequest, IAlternateAuthorizedScope
    {
        public string ScopeType => nameof(RoleScopeType.Tournament);
        public Guid ScopeId => TournamentId;
        public IReadOnlyCollection<string> AllowedRoles => MatchRoles.AnyTournamentMember;

        public string AlternateScopeType => nameof(RoleScopeType.Match);
        public Guid AlternateScopeId => MatchId;
        public IReadOnlyCollection<string> AlternateAllowedRoles => MatchRoles.RefereeOnly;
    }
}

using Turniejo.Matches.Application.Authorization;
using Turniejo.Matches.Application.DTOs;
using Turniejo.SharedKernel.CQRS;
using Turniejo.SharedKernel.Enums;

namespace Turniejo.Matches.Application.Stats.GetTopScorers
{
    public sealed record GetTopScorersQuery(Guid TournamentId, int Top = 10)
        : IQuery<IReadOnlyList<PlayerRankingEntryDto>>, IAuthorizedRequest
    {
        public string ScopeType => nameof(RoleScopeType.Tournament);
        public Guid ScopeId => TournamentId;
        public IReadOnlyCollection<string> AllowedRoles => MatchRoles.AnyTournamentMember;
    }
}

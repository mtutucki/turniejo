using Turniejo.SharedKernel.CQRS;
using Turniejo.SharedKernel.Enums;
using Turniejo.Standings.Application.Authorization;
using Turniejo.Standings.Application.DTOs;

namespace Turniejo.Standings.Application.Standings.GetStandings
{
    public sealed record GetStandingsQuery(Guid TournamentId, Guid GroupId) : IQuery<IReadOnlyList<StandingDto>>, IAuthorizedRequest
    {
        public string ScopeType => nameof(RoleScopeType.Tournament);
        public Guid ScopeId => TournamentId;
        public IReadOnlyCollection<string> AllowedRoles => StandingRoles.AnyTournamentMember;
    }
}

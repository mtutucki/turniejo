using Turniejo.Matches.Application.Authorization;
using Turniejo.Matches.Application.DTOs;
using Turniejo.SharedKernel.CQRS;
using Turniejo.SharedKernel.Enums;

namespace Turniejo.Matches.Application.Pitches.GetTournamentPitches
{
    public sealed record GetTournamentPitchesQuery(Guid TournamentId) : IQuery<IReadOnlyList<PitchDto>>, IAuthorizedRequest
    {
        public string ScopeType => nameof(RoleScopeType.Tournament);
        public Guid ScopeId => TournamentId;
        public IReadOnlyCollection<string> AllowedRoles => MatchRoles.AnyTournamentMember;
    }
}

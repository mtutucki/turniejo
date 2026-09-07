using Turniejo.Players.Application.Authorization;
using Turniejo.Players.Application.DTOs;
using Turniejo.SharedKernel.CQRS;
using Turniejo.SharedKernel.Enums;

namespace Turniejo.Players.Application.TournamentPlayers.GetTournamentTeamPlayers
{
    public sealed record GetTournamentTeamPlayersQuery(Guid TournamentId, Guid TournamentTeamId)
        : IQuery<IReadOnlyList<TournamentPlayerDto>>, IAuthorizedRequest
    {
        public string ScopeType => nameof(RoleScopeType.Tournament);
        public Guid ScopeId => TournamentId;
        public IReadOnlyCollection<string> AllowedRoles => PlayerRoles.AnyTournamentMember;
    }
}

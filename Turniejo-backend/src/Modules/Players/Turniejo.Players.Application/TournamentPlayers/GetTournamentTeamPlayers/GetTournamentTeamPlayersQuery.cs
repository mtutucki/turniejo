using Turniejo.Players.Application.Authorization;
using Turniejo.Players.Application.DTOs;
using Turniejo.SharedKernel.CQRS;
using Turniejo.SharedKernel.Enums;

namespace Turniejo.Players.Application.TournamentPlayers.GetTournamentTeamPlayers
{
    public sealed record GetTournamentTeamPlayersQuery(Guid TournamentId, Guid TournamentTeamId)
        : IQuery<IReadOnlyList<TournamentPlayerDto>>, IAuthorizedRequest, IAlternateAuthorizedScope
    {
        public string ScopeType => nameof(RoleScopeType.Tournament);
        public Guid ScopeId => TournamentId;
        public IReadOnlyCollection<string> AllowedRoles => PlayerRoles.AnyTournamentMember;

        public string AlternateScopeType => nameof(RoleScopeType.TournamentTeam);
        public Guid AlternateScopeId => TournamentTeamId;
        public IReadOnlyCollection<string> AlternateAllowedRoles => PlayerRoles.CoachOnly;
    }
}

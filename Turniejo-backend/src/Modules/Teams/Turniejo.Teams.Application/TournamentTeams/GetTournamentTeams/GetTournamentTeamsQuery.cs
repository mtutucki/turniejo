using Turniejo.SharedKernel.CQRS;
using Turniejo.SharedKernel.Enums;
using Turniejo.Teams.Application.Authorization;
using Turniejo.Teams.Application.DTOs;

namespace Turniejo.Teams.Application.TournamentTeams.GetTournamentTeams
{
    public sealed record GetTournamentTeamsQuery(Guid TournamentId) : IQuery<IReadOnlyList<TournamentTeamDto>>, IAuthorizedRequest
    {
        public string ScopeType => nameof(RoleScopeType.Tournament);
        public Guid ScopeId => TournamentId;
        public IReadOnlyCollection<string> AllowedRoles => TeamRoles.AnyTournamentMember;
    }
}

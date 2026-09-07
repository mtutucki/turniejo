using Turniejo.SharedKernel.CQRS;
using Turniejo.SharedKernel.Enums;
using Turniejo.Tournaments.Application.Authorization;
using Turniejo.Tournaments.Application.DTOs;

namespace Turniejo.Tournaments.Application.Tournaments.GetTournamentById
{
    public sealed record GetTournamentByIdQuery(Guid TournamentId) : IQuery<TournamentDto>, IAuthorizedRequest
    {
        public string ScopeType => nameof(RoleScopeType.Tournament);
        public Guid ScopeId => TournamentId;
        public IReadOnlyCollection<string> AllowedRoles => TournamentRoles.AnyMember;
    }
}

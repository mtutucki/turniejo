using Turniejo.Players.Application.Authorization;
using Turniejo.Players.Application.DTOs;
using Turniejo.SharedKernel.CQRS;
using Turniejo.SharedKernel.Enums;

namespace Turniejo.Players.Application.PlayerGuardians.GetPlayerGuardians
{
    public sealed record GetPlayerGuardiansQuery(Guid PlayerId) : IQuery<IReadOnlyList<PlayerGuardianDto>>, IAuthorizedRequest
    {
        public string ScopeType => nameof(RoleScopeType.Player);
        public Guid ScopeId => PlayerId;
        public IReadOnlyCollection<string> AllowedRoles => PlayerRoles.GuardianOnly;
    }
}

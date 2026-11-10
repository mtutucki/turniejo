using Turniejo.Brackets.Application.Authorization;
using Turniejo.Brackets.Application.DTOs;
using Turniejo.SharedKernel.CQRS;
using Turniejo.SharedKernel.Enums;

namespace Turniejo.Brackets.Application.SecondStage.GetSecondStage
{
    public sealed record GetSecondStageQuery(Guid TournamentId) : IQuery<SecondStageDto>, IAuthorizedRequest
    {
        public string ScopeType => nameof(RoleScopeType.Tournament);
        public Guid ScopeId => TournamentId;
        public IReadOnlyCollection<string> AllowedRoles => BracketRoles.AnyTournamentMember;
    }
}

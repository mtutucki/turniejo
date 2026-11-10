using Turniejo.Brackets.Application.Authorization;
using Turniejo.Brackets.Application.DTOs;
using Turniejo.SharedKernel.CQRS;
using Turniejo.SharedKernel.Enums;

namespace Turniejo.Brackets.Application.SecondStage.GetSecondStageSeats
{
    public sealed record GetSecondStageSeatsQuery(Guid TournamentId) : IQuery<IReadOnlyList<SecondStageSeatDto>>, IAuthorizedRequest
    {
        public string ScopeType => nameof(RoleScopeType.Tournament);
        public Guid ScopeId => TournamentId;
        public IReadOnlyCollection<string> AllowedRoles => BracketRoles.AnyTournamentMember;
    }
}

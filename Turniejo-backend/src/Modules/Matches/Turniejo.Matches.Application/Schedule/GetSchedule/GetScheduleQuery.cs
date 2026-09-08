using Turniejo.Matches.Application.Authorization;
using Turniejo.Matches.Application.DTOs;
using Turniejo.SharedKernel.CQRS;
using Turniejo.SharedKernel.Enums;

namespace Turniejo.Matches.Application.Schedule.GetSchedule
{
    public sealed record GetScheduleQuery(Guid TournamentId) : IQuery<IReadOnlyList<MatchDto>>, IAuthorizedRequest
    {
        public string ScopeType => nameof(RoleScopeType.Tournament);
        public Guid ScopeId => TournamentId;
        public IReadOnlyCollection<string> AllowedRoles => MatchRoles.AnyTournamentMember;
    }
}

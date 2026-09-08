using Turniejo.Matches.Application.Authorization;
using Turniejo.SharedKernel.CQRS;
using Turniejo.SharedKernel.Enums;

namespace Turniejo.Matches.Application.Matches.UpdateMatchSchedule
{
    public sealed record UpdateMatchScheduleCommand(
        Guid TournamentId,
        Guid MatchId,
        Guid? PitchId,
        DateTimeOffset ScheduledAt) : ICommand<UpdateMatchScheduleResponse>, IAuthorizedRequest
    {
        public string ScopeType => nameof(RoleScopeType.Tournament);
        public Guid ScopeId => TournamentId;
        public IReadOnlyCollection<string> AllowedRoles => MatchRoles.OrganizerOnly;
    }
}

using Turniejo.Matches.Application.Authorization;
using Turniejo.SharedKernel.CQRS;
using Turniejo.SharedKernel.Enums;

namespace Turniejo.Matches.Application.Matches.UpdatePlannedMatchSchedule
{
    /// <summary>Termin i boisko meczu pucharu, który jeszcze nie powstał (węzeł drabinki).</summary>
    public sealed record UpdatePlannedMatchScheduleCommand(
        Guid TournamentId,
        Guid NodeId,
        Guid PitchId,
        DateTimeOffset ScheduledAt) : ICommand<UpdatePlannedMatchScheduleResponse>, IAuthorizedRequest
    {
        public string ScopeType => nameof(RoleScopeType.Tournament);
        public Guid ScopeId => TournamentId;
        public IReadOnlyCollection<string> AllowedRoles => MatchRoles.OrganizerOnly;
    }

    public sealed record UpdatePlannedMatchScheduleResponse(Guid NodeId);
}

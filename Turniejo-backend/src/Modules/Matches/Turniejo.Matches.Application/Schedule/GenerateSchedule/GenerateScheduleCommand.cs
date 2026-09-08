using Turniejo.Matches.Application.Authorization;
using Turniejo.SharedKernel.CQRS;
using Turniejo.SharedKernel.Enums;

namespace Turniejo.Matches.Application.Schedule.GenerateSchedule
{
    public sealed record GenerateScheduleCommand(
        Guid TournamentId,
        DateTimeOffset StartAt,
        DateTimeOffset EndAt,
        IReadOnlyList<Guid> PitchIds) : ICommand<GenerateScheduleResponse>, IAuthorizedRequest
    {
        public string ScopeType => nameof(RoleScopeType.Tournament);
        public Guid ScopeId => TournamentId;
        public IReadOnlyCollection<string> AllowedRoles => MatchRoles.OrganizerOnly;
    }
}

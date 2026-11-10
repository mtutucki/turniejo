using Turniejo.Matches.Application.Authorization;
using Turniejo.SharedKernel.CQRS;
using Turniejo.SharedKernel.Enums;

namespace Turniejo.Matches.Application.Matches.UpdateMatchSchedule
{
    public sealed record UpdateMatchScheduleCommand(
        Guid TournamentId,
        Guid MatchId,
        Guid? PitchId,
        DateTimeOffset ScheduledAt,
        // Opóźnienie na boisku: późniejsze, nierozpoczęte mecze na tym samym boisku przesuwają się o tyle samo.
        bool ShiftFollowing = false,
        // Organizator potwierdził, że przesunięcie rusza też mecze na innych boiskach.
        bool ShiftOtherPitches = false) : ICommand<UpdateMatchScheduleResponse>, IAuthorizedRequest
    {
        public string ScopeType => nameof(RoleScopeType.Tournament);
        public Guid ScopeId => TournamentId;
        public IReadOnlyCollection<string> AllowedRoles => MatchRoles.OrganizerOnly;
    }
}

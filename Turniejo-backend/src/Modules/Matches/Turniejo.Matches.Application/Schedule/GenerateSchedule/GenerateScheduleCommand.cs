using Turniejo.Matches.Application.Authorization;
using Turniejo.SharedKernel.CQRS;
using Turniejo.SharedKernel.Enums;

namespace Turniejo.Matches.Application.Schedule.GenerateSchedule
{
    // Okno gry jednego dnia turnieju (od pierwszego meczu do końca ostatniego).
    // MaxMatches: organizator decyduje, ile meczów gra się danego dnia — generator nie dołoży więcej,
    // nawet gdy w oknie zostają wolne godziny. Puste = tyle, ile się zmieści.
    public sealed record ScheduleWindow(DateTimeOffset StartAt, DateTimeOffset EndAt, int? MaxMatches = null);

    // Windows: turniej wielodniowy podaje osobne okno na każdy dzień gry — bez tego mecze trafiałyby
    // w noc między dniami. Puste = jedno okno StartAt–EndAt.
    public sealed record GenerateScheduleCommand(
        Guid TournamentId,
        DateTimeOffset StartAt,
        DateTimeOffset EndAt,
        IReadOnlyList<Guid> PitchIds,
        IReadOnlyList<ScheduleWindow>? Windows = null) : ICommand<GenerateScheduleResponse>, IAuthorizedRequest
    {
        public string ScopeType => nameof(RoleScopeType.Tournament);
        public Guid ScopeId => TournamentId;
        public IReadOnlyCollection<string> AllowedRoles => MatchRoles.OrganizerOnly;

        public IReadOnlyList<ScheduleWindow> EffectiveWindows =>
            Windows is { Count: > 0 }
                ? Windows.OrderBy(w => w.StartAt).ToList()
                : [new ScheduleWindow(StartAt, EndAt)];
    }
}

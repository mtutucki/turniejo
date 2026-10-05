using Turniejo.Matches.Application.Authorization;
using Turniejo.SharedKernel.CQRS;
using Turniejo.SharedKernel.Enums;

namespace Turniejo.Matches.Application.Schedule.ClearSchedule
{
    // Generowanie jest jednorazowe (ScheduleGenerated), więc bez tej komendy pomyłka organizatora —
    // np. drużyna zaakceptowana po generowaniu — nie miała naprawy innej niż ręczne przesuwanie meczów.
    public sealed record ClearScheduleCommand(Guid TournamentId) : ICommand<ClearScheduleResponse>, IAuthorizedRequest
    {
        public string ScopeType => nameof(RoleScopeType.Tournament);
        public Guid ScopeId => TournamentId;
        public IReadOnlyCollection<string> AllowedRoles => MatchRoles.OrganizerOnly;
    }

    public sealed record ClearScheduleResponse(int MatchesRemoved);
}

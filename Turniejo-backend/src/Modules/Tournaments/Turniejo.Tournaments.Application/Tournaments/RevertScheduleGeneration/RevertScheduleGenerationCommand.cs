using Turniejo.SharedKernel.CQRS;
using Turniejo.SharedKernel.Enums;
using Turniejo.Tournaments.Application.Authorization;

namespace Turniejo.Tournaments.Application.Tournaments.RevertScheduleGeneration
{
    // Wywoływane wyłącznie przez ITournamentModule z ClearScheduleCommand (moduł Matches) — bez endpointu,
    // bo samo cofnięcie etapu bez usunięcia meczów zostawiłoby mecze w turnieju z otwartą edycją grup.
    public sealed record RevertScheduleGenerationCommand(Guid TournamentId) : ICommand, IAuthorizedRequest
    {
        public string ScopeType => nameof(RoleScopeType.Tournament);
        public Guid ScopeId => TournamentId;
        public IReadOnlyCollection<string> AllowedRoles => TournamentRoles.OrganizerOnly;
    }
}

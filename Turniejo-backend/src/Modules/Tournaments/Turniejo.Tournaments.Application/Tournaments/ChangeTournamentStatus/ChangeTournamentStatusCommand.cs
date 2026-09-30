using Turniejo.SharedKernel.CQRS;
using Turniejo.SharedKernel.Enums;
using Turniejo.Tournaments.Application.Authorization;
using Turniejo.Tournaments.Domain.Enums;

namespace Turniejo.Tournaments.Application.Tournaments.ChangeTournamentStatus
{
    // IsScheduleTransition ustawia tylko ITournamentModule (generator terminarza) — kontroler buduje komendę
    // z dwóch pól żądania, więc z API nie da się go przekazać.
    public sealed record ChangeTournamentStatusCommand(Guid TournamentId, TournamentStatus TournamentStatus, bool IsScheduleTransition = false)
        : ICommand<ChangeTournamentStatusResponse>, IAuthorizedRequest
    {
        public string ScopeType => nameof(RoleScopeType.Tournament);
        public Guid ScopeId => TournamentId;
        public IReadOnlyCollection<string> AllowedRoles => TournamentRoles.OrganizerOnly;
    }
}

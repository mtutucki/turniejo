using Turniejo.SharedKernel.CQRS;
using Turniejo.SharedKernel.Enums;
using Turniejo.Tournaments.Application.Authorization;
using Turniejo.Tournaments.Domain.Enums;

namespace Turniejo.Tournaments.Application.Tournaments.ChangeTournamentStatus
{
    public sealed record ChangeTournamentStatusCommand(Guid TournamentId, TournamentStatus TournamentStatus)
        : ICommand<ChangeTournamentStatusResponse>, IAuthorizedRequest
    {
        public string ScopeType => nameof(RoleScopeType.Tournament);
        public Guid ScopeId => TournamentId;
        public IReadOnlyCollection<string> AllowedRoles => TournamentRoles.OrganizerOnly;
    }
}

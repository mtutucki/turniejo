using Turniejo.SharedKernel.CQRS;
using Turniejo.SharedKernel.Enums;
using Turniejo.Tournaments.Application.Authorization;
using Turniejo.Tournaments.Domain.Enums;

namespace Turniejo.Tournaments.Application.Tournaments.SetTournamentFormat
{
    public sealed record SetTournamentFormatCommand(Guid TournamentId, TournamentFormat TournamentFormat)
        : ICommand<SetTournamentFormatResponse>, IAuthorizedRequest
    {
        public string ScopeType => nameof(RoleScopeType.Tournament);
        public Guid ScopeId => TournamentId;
        public IReadOnlyCollection<string> AllowedRoles => TournamentRoles.OrganizerOnly;
    }
}

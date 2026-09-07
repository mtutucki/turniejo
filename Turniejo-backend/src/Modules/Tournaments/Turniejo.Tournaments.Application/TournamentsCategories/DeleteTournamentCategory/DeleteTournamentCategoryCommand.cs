
using Turniejo.SharedKernel.CQRS;
using Turniejo.SharedKernel.Enums;
using Turniejo.Tournaments.Application.Authorization;

namespace Turniejo.Tournaments.Application.TournamentsCategories.DeleteTournamentCategory
{
    public sealed record DeleteTournamentCategoryCommand(Guid TournamentId, Guid CategoryId) : ICommand, IAuthorizedRequest
    {
        public string ScopeType => nameof(RoleScopeType.Tournament);
        public Guid ScopeId => TournamentId;
        public IReadOnlyCollection<string> AllowedRoles => TournamentRoles.OrganizerOnly;
    }
}

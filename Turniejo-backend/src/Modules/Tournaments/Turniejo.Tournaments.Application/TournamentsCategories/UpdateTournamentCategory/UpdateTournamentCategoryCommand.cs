using Turniejo.SharedKernel.CQRS;
using Turniejo.SharedKernel.Enums;
using Turniejo.Tournaments.Application.Authorization;

namespace Turniejo.Tournaments.Application.TournamentsCategories.UpdateTournamentCategory
{
    public sealed record UpdateTournamentCategoryCommand(
        Guid TournamentId,
        Guid CategoryId,
        string Name,
        DateTime? MinBirthDate,
        DateTime? MaxBirthDate,
        int Order) : ICommand<UpdateTournamentCategoryResponse>, IAuthorizedRequest
    {
        public string ScopeType => nameof(RoleScopeType.Tournament);
        public Guid ScopeId => TournamentId;
        public IReadOnlyCollection<string> AllowedRoles => TournamentRoles.OrganizerOnly;
    }
}

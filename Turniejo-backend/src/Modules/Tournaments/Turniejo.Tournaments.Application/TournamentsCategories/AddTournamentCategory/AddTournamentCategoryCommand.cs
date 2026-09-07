using Turniejo.SharedKernel.CQRS;
using Turniejo.SharedKernel.Enums;
using Turniejo.Tournaments.Application.Authorization;

namespace Turniejo.Tournaments.Application.TournamentsCategories.AddTournamentCategory
{
    public sealed record AddTournamentCategoryCommand(
        Guid TournamentId,
        string Name,
        DateTime? MinBirthDate,
        DateTime? MaxBirthDate,
        int Order) : ICommand<AddTournamentCategoryResponse>, IAuthorizedRequest
    {
        public string ScopeType => nameof(RoleScopeType.Tournament);
        public Guid ScopeId => TournamentId;
        public IReadOnlyCollection<string> AllowedRoles => TournamentRoles.OrganizerOnly;
    }
}

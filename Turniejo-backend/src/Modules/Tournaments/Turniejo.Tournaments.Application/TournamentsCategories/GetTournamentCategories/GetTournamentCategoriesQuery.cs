using Turniejo.SharedKernel.CQRS;
using Turniejo.SharedKernel.Enums;
using Turniejo.Tournaments.Application.Authorization;
using Turniejo.Tournaments.Application.DTOs;

namespace Turniejo.Tournaments.Application.TournamentsCategories.GetTournamentCategories
{
    public sealed record GetTournamentCategoriesQuery(Guid TournamentId) : IQuery<IReadOnlyList<TournamentCategoryDto>>, IAuthorizedRequest
    {
        public string ScopeType => nameof(RoleScopeType.Tournament);
        public Guid ScopeId => TournamentId;
        public IReadOnlyCollection<string> AllowedRoles => TournamentRoles.AnyMember;
    }
}
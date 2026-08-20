using Turniejo.SharedKernel.CQRS;
using Turniejo.Tournaments.Application.DTOs;

namespace Turniejo.Tournaments.Application.TournamentsCategories.GetTournamentCategories
{
    public sealed record GetTournamentCategoriesQuery(Guid TournamentId) : IQuery<IReadOnlyList<TournamentCategoryDto>>;
}
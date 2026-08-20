using Turniejo.SharedKernel.CQRS;
using Turniejo.SharedKernel.Results;
using Turniejo.Tournaments.Application.Abstractions;
using Turniejo.Tournaments.Application.DTOs;

namespace Turniejo.Tournaments.Application.TournamentsCategories.GetTournamentCategories
{
    internal sealed class GetTournamentCategoriesQueryHandler(ITournamentsDbContext dbContext) : IQueryHandler<GetTournamentCategoriesQuery, IReadOnlyList<TournamentCategoryDto>>
    {
        public async Task<Result<IReadOnlyList<TournamentCategoryDto>>> Handle(GetTournamentCategoriesQuery request, CancellationToken cancellationToken)
        {
            var categories = await dbContext.GetTournamentCategoriesByTournamentIdAsync(request.TournamentId, cancellationToken);

            var dtos = categories.Select(c => new TournamentCategoryDto(
                c.Id, c.TournamentId, c.Name, c.MinBirthDate, c.MaxBirthDate, c.Order)).ToList();

            return Result.Success<IReadOnlyList<TournamentCategoryDto>>(dtos);
        }
    }
}
using Turniejo.SharedKernel.CQRS;
using Turniejo.SharedKernel.Results;
using Turniejo.Tournaments.Application.Abstractions;

namespace Turniejo.Tournaments.Application.TournamentsCategories.DeleteTournamentCategory
{
    internal sealed class DeleteTournamentCategoryCommandHandler(ITournamentsDbContext tournamentsDbContext) : ICommandHandler<DeleteTournamentCategoryCommand>
    {
        public async Task<Result> Handle(DeleteTournamentCategoryCommand request, CancellationToken cancellationToken)
        {
            var category = await tournamentsDbContext.GetTournamentCategoryByIdAsync(request.CategoryId, cancellationToken);
            if (category == null)
                return Result.Failure(Error.NotFound("TournamentCategory", request.CategoryId));

            await tournamentsDbContext.RemoveTournamentCategoryAsync(category, cancellationToken);
            await tournamentsDbContext.SaveChangesAsync(cancellationToken);

            return Result.Success();
        }
    }
}

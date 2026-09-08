using Turniejo.SharedKernel.CQRS;
using Turniejo.SharedKernel.Contracts;
using Turniejo.SharedKernel.Results;
using Turniejo.Tournaments.Application.Abstractions;

namespace Turniejo.Tournaments.Application.TournamentsCategories.DeleteTournamentCategory
{
    // CategoryId nie ma FK do Teams/Matches (moduły nie dzielą DbContextu ani schematu), więc bez tych
    // kontroli kasowanie kategorii cicho osierocało TournamentTeam.CategoryId/Match.CategoryId w innych
    // modułach — łamie to grupowanie w tabeli, drabinki, filtry UI, bez żadnego błędu.
    internal sealed class DeleteTournamentCategoryCommandHandler(
        ITournamentsDbContext tournamentsDbContext,
        ITeamModule teamModule,
        IMatchModule matchModule) : ICommandHandler<DeleteTournamentCategoryCommand>
    {
        public async Task<Result> Handle(DeleteTournamentCategoryCommand request, CancellationToken cancellationToken)
        {
            var category = await tournamentsDbContext.GetTournamentCategoryByIdAsync(request.CategoryId, cancellationToken);
            if (category == null || category.TournamentId != request.TournamentId)
                return Result.Failure(Error.NotFound("TournamentCategory", request.CategoryId));

            if (await teamModule.AnyTeamsInCategoryAsync(request.CategoryId, cancellationToken))
                return Result.Failure(Error.Conflict("CategoryId", "Cannot delete a category that still has teams registered to it"));

            if (await matchModule.AnyMatchesForCategoryAsync(request.CategoryId, cancellationToken))
                return Result.Failure(Error.Conflict("CategoryId", "Cannot delete a category that still has matches scheduled in it"));

            await tournamentsDbContext.RemoveTournamentCategoryAsync(category, cancellationToken);
            await tournamentsDbContext.SaveChangesAsync(cancellationToken);

            return Result.Success();
        }
    }
}

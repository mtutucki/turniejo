using Turniejo.SharedKernel.CQRS;
using Turniejo.SharedKernel.Contracts;
using Turniejo.SharedKernel.Enums;
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
        IMatchModule matchModule,
        IPlayerModule playerModule,
        IUserModule userModule) : ICommandHandler<DeleteTournamentCategoryCommand>
    {
        public async Task<Result> Handle(DeleteTournamentCategoryCommand request, CancellationToken cancellationToken)
        {
            var category = await tournamentsDbContext.GetTournamentCategoryByIdAsync(request.CategoryId, cancellationToken);
            if (category == null || category.TournamentId != request.TournamentId)
                return Result.Failure(Error.NotFound("TournamentCategory", request.CategoryId));

            if (await teamModule.AnyActiveTeamsInCategoryAsync(request.CategoryId, cancellationToken))
                return Result.Failure(Error.Conflict("CategoryId", "Cannot delete a category that still has teams registered to it"));

            if (await matchModule.AnyMatchesForCategoryAsync(request.CategoryId, cancellationToken))
                return Result.Failure(Error.Conflict("CategoryId", "Cannot delete a category that still has matches scheduled in it"));

            // Odrzucone i wycofane zgłoszenia lista drużyn ukrywa, więc kategoria wygląda na pustą — usuwamy je
            // razem z nią. Moduły nie dzielą transakcji: kolejność jak przy usuwaniu organizacji (składy przed
            // zgłoszeniami, kategoria na końcu), żeby przerwane usuwanie dało się po prostu powtórzyć.
            var inactiveTeamIds = await teamModule.GetInactiveTournamentTeamIdsByCategoryIdAsync(category.Id, cancellationToken);
            if (inactiveTeamIds.Count > 0)
            {
                var removedPlayerIds = await playerModule.RemovePlayersOfTournamentTeamsAsync(inactiveTeamIds, cancellationToken);
                await userModule.RemoveRolesByScopesAsync(nameof(RoleScopeType.TournamentTeam), inactiveTeamIds, cancellationToken);
                await userModule.RemoveRolesByScopesAsync(nameof(RoleScopeType.Player), removedPlayerIds, cancellationToken);
                await teamModule.RemoveTournamentTeamsAsync(inactiveTeamIds, cancellationToken);
            }

            // Grupy należą do kategorii (FK Restrict), a skoro nie ma w niej drużyn ani meczów, to i w jej
            // grupach ich nie ma — kasujemy je razem z kategorią w jednej transakcji SaveChanges.
            var groups = await tournamentsDbContext.GetTournamentGroupsByCategoryIdAsync(category.Id, cancellationToken);
            foreach (var group in groups)
                await tournamentsDbContext.RemoveTournamentGroupAsync(group, cancellationToken);

            await tournamentsDbContext.RemoveTournamentCategoryAsync(category, cancellationToken);
            await tournamentsDbContext.SaveChangesAsync(cancellationToken);

            return Result.Success();
        }
    }
}

using Turniejo.SharedKernel.CQRS;
using Turniejo.SharedKernel.Results;
using Turniejo.Tournaments.Application.Abstractions;

namespace Turniejo.Tournaments.Application.TournamentsCategories.UpdateTournamentCategory
{
    internal sealed class UpdateTournamentCategoryCommandHandler(ITournamentsDbContext tournamentsDbContext) : ICommandHandler<UpdateTournamentCategoryCommand, UpdateTournamentCategoryResponse>

    {
        public async Task<Result<UpdateTournamentCategoryResponse>> Handle(UpdateTournamentCategoryCommand request, CancellationToken cancellationToken)
        {
            var tournamentCategory = await tournamentsDbContext.GetTournamentCategoryByIdAsync(request.CategoryId, cancellationToken);
            if (tournamentCategory == null || tournamentCategory.TournamentId != request.TournamentId)
                return Result.Failure<UpdateTournamentCategoryResponse>(Error.NotFound("TournamentCategory", request.CategoryId));

            var updateResult = tournamentCategory.UpdateDetails(request.Name, request.MinBirthDate, request.MaxBirthDate, request.Order);
            if (updateResult.IsFailure)
                return Result.Failure<UpdateTournamentCategoryResponse>(updateResult.Error);

            await tournamentsDbContext.SaveChangesAsync(cancellationToken);
            return Result.Success(new UpdateTournamentCategoryResponse(tournamentCategory.Id));
        }
    }
}

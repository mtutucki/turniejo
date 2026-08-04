using Turniejo.SharedKernel.CQRS;
using Turniejo.SharedKernel.Results;
using Turniejo.Tournaments.Application.Abstractions;

namespace Turniejo.Tournaments.Application.Tournaments.UpdateTournament
{
    internal sealed class UpdateTournamentDetailsCommandHandler(ITournamentsDbContext dbContext) : ICommandHandler<UpdateTournamentDetailsCommand, UpdateTournamentDetailsResponse>
    {
        public async Task<Result<UpdateTournamentDetailsResponse>> Handle(UpdateTournamentDetailsCommand request, CancellationToken cancellationToken)
        {
            var result = await dbContext.GetTournamentByIdAsync(request.TournamentId, cancellationToken);

            if (result == null)
                return Result.Failure<UpdateTournamentDetailsResponse>(Error.NotFound("Tournament", request.TournamentId));

            var updateResult = result.UpdateDetails(request.Name, request.StartAt, request.EndAt, request.TeamLimit, request.Description);

            if (updateResult.IsFailure)
                return Result.Failure<UpdateTournamentDetailsResponse>(updateResult.Error);


            await dbContext.SaveChangesAsync(cancellationToken);
            return Result.Success(new UpdateTournamentDetailsResponse(result.Id, result.Slug));
        }
    }
}

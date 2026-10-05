using Turniejo.SharedKernel.CQRS;
using Turniejo.SharedKernel.Results;
using Turniejo.Tournaments.Application.Abstractions;

namespace Turniejo.Tournaments.Application.Tournaments.DeleteTournament
{
    internal sealed class DeleteTournamentCommandHandler(ITournamentsDbContext dbContext)
        : ICommandHandler<DeleteTournamentCommand, DeleteTournamentResponse>
    {
        public async Task<Result<DeleteTournamentResponse>> Handle(DeleteTournamentCommand request, CancellationToken cancellationToken)
        {
            var tournament = await dbContext.GetTournamentByIdAsync(request.TournamentId, cancellationToken);
            if (tournament == null)
                return Result.Failure<DeleteTournamentResponse>(Error.NotFound("Tournament", request.TournamentId));

            var result = tournament.Delete();
            if (result.IsFailure)
                return Result.Failure<DeleteTournamentResponse>(result.Error);

            await dbContext.SaveChangesAsync(cancellationToken);
            return Result.Success(new DeleteTournamentResponse(tournament.Id));
        }
    }
}

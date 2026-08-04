using Turniejo.SharedKernel.CQRS;
using Turniejo.SharedKernel.Results;
using Turniejo.Tournaments.Application.Abstractions;

namespace Turniejo.Tournaments.Application.Tournaments.SetTournamentFormat
{
    public sealed class SetTournamentFormatCommandHandler(ITournamentsDbContext tournamentsDbContext) : ICommandHandler<SetTournamentFormatCommand, SetTournamentFormatResponse>
    {
        public async Task<Result<SetTournamentFormatResponse>> Handle(SetTournamentFormatCommand request, CancellationToken cancellationToken)
        {
            var result = await tournamentsDbContext.GetTournamentByIdAsync(request.TournamentId, cancellationToken);

            if (result == null)
                return Result.Failure<SetTournamentFormatResponse>(Error.NotFound("Tournament", request.TournamentId));

            var setFormatResult = result.SetFormat(request.TournamentFormat);

            if (setFormatResult.IsFailure)
                return Result.Failure<SetTournamentFormatResponse>(setFormatResult.Error);

            await tournamentsDbContext.SaveChangesAsync(cancellationToken);
            return Result.Success(new SetTournamentFormatResponse(result.Id, result.Slug));

        }
    }
}

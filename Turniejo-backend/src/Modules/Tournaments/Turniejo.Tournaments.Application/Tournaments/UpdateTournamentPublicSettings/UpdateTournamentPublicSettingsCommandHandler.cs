using Turniejo.SharedKernel.CQRS;
using Turniejo.SharedKernel.Results;
using Turniejo.Tournaments.Application.Abstractions;

namespace Turniejo.Tournaments.Application.Tournaments.UpdateTournamentPublicSettings
{
    internal sealed class UpdateTournamentPublicSettingsCommandHandler(ITournamentsDbContext dbContext)
        : ICommandHandler<UpdateTournamentPublicSettingsCommand, UpdateTournamentPublicSettingsResponse>
    {
        public async Task<Result<UpdateTournamentPublicSettingsResponse>> Handle(UpdateTournamentPublicSettingsCommand request, CancellationToken cancellationToken)
        {
            var tournament = await dbContext.GetTournamentByIdAsync(request.TournamentId, cancellationToken);
            if (tournament == null)
                return Result.Failure<UpdateTournamentPublicSettingsResponse>(Error.NotFound("Tournament", request.TournamentId));

            var updateResult = tournament.UpdatePublicSettings(
                request.IsPublic, request.ShowTeams, request.ShowPlayers, request.ShowSchedule,
                request.ShowStandings, request.ShowBracket, request.ShowLiveMatches);

            if (updateResult.IsFailure)
                return Result.Failure<UpdateTournamentPublicSettingsResponse>(updateResult.Error);

            await dbContext.SaveChangesAsync(cancellationToken);
            return Result.Success(new UpdateTournamentPublicSettingsResponse(tournament.Id));
        }
    }
}

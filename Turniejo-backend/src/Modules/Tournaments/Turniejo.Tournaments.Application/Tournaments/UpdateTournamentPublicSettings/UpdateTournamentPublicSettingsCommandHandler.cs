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

            if (request.Expected is { } expected)
            {
                var p = tournament.PublicSettings;
                var current = new ExpectedPublicSettings(
                    p.IsPublic, p.ShowTeams, p.ShowPlayers, p.ShowSchedule, p.ShowStandings, p.ShowBracket,
                    p.ShowLiveMatches, p.ShowStats, p.AbbreviatePlayerNames, p.ShowInDirectory);
                if (current != expected)
                    return Result.Failure<UpdateTournamentPublicSettingsResponse>(Error.Conflict(
                        "PublicSettings", "The portal settings were changed by someone else. Refresh to see the latest data."));
            }

            var updateResult = tournament.UpdatePublicSettings(
                request.IsPublic, request.ShowTeams, request.ShowPlayers, request.ShowSchedule,
                request.ShowStandings, request.ShowBracket, request.ShowLiveMatches, request.ShowStats,
                request.AbbreviatePlayerNames, request.ShowInDirectory);

            if (updateResult.IsFailure)
                return Result.Failure<UpdateTournamentPublicSettingsResponse>(updateResult.Error);

            await dbContext.SaveChangesAsync(cancellationToken);
            return Result.Success(new UpdateTournamentPublicSettingsResponse(tournament.Id));
        }
    }
}

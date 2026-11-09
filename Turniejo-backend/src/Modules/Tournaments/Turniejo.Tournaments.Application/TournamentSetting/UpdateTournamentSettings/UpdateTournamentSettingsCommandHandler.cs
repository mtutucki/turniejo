using Turniejo.SharedKernel.Contracts;
using Turniejo.SharedKernel.CQRS;
using Turniejo.SharedKernel.Results;
using Turniejo.Tournaments.Application.Abstractions;
using Turniejo.Tournaments.Application.TournamentSetting.UpdateTournamentSettings;

namespace Turniejo.Tournaments.Application.Tournaments.TournamentSetting.UpdateTournamentSettings
{
    internal sealed class UpdateTournamentSettingsCommandHandler(ITournamentsDbContext dbContext, ICurrentUserContext currentUserContext) : ICommandHandler<UpdateTournamentSettingsCommand, UpdateTournamentSettingsResponse>
    {
        public async Task<Result<UpdateTournamentSettingsResponse>> Handle(UpdateTournamentSettingsCommand request, CancellationToken cancellationToken)
        {
            var tournament = await dbContext.GetTournamentByIdAsync(request.TournamentId, cancellationToken);
            if (tournament == null)
                return Result.Failure<UpdateTournamentSettingsResponse>(Error.NotFound("Tournament", request.TournamentId));

            if (request.Expected is { } expected)
            {
                var s = tournament.Settings;
                var current = new ExpectedTournamentSettings(
                    s.PointsForWin, s.PointsForDraw, s.PointsForLoss, s.MatchDurationMinutes, s.BreakDurationMinutes,
                    s.IsThirdPlaceMatchEnabled, s.RefereeEditWindowMinutes, s.WalkoverHomeScore, s.WalkoverAwayScore,
                    s.CanCoachEditSquadAfterStart);
                if (current != expected)
                    return Result.Failure<UpdateTournamentSettingsResponse>(Error.Conflict(
                        "Settings", "The rules were changed by someone else. Refresh to see the latest data."));
            }

            var updateResult = tournament.UpdateSettings(
                request.PointsForWin, request.PointsForDraw, request.PointsForLoss, request.MatchDurationMinutes, request.BreakDurationMinutes, request.IsThirdPlaceMatchEnabled,
                request.RefereeEditWindowMinutes, request.WalkoverHomeScore, request.WalkoverAwayScore, request.CanCoachEditSquadAfterStart, currentUserContext.UserId
                );

            if (updateResult.IsFailure)
                return Result.Failure<UpdateTournamentSettingsResponse>(updateResult.Error);

            await dbContext.SaveChangesAsync(cancellationToken);
            return Result.Success(new UpdateTournamentSettingsResponse(tournament.Id));
        }
    }
}

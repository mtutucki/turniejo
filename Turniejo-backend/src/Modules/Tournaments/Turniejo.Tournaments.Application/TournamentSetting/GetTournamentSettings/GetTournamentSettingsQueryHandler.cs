using Turniejo.SharedKernel.CQRS;
using Turniejo.SharedKernel.Results;
using Turniejo.Tournaments.Application.Abstractions;
using Turniejo.Tournaments.Application.DTOs;

namespace Turniejo.Tournaments.Application.TournamentSetting.GetTournamentSettings
{
    public sealed class GetTournamentSettingsQueryHandler(ITournamentsDbContext dbContext) : IQueryHandler<GetTournamentSettingsQuery, TournamentSettingsDto>
    {
        public async Task<Result<TournamentSettingsDto>> Handle(GetTournamentSettingsQuery request, CancellationToken cancellationToken)
        {
            var tournament = await dbContext.GetTournamentByIdAsync(request.TournamentId, cancellationToken);
            if (tournament == null)
                return Result.Failure<TournamentSettingsDto>(Error.NotFound("Tournament", request.TournamentId));

            var tournamentSettingsDto = new TournamentSettingsDto(
                tournament.Settings.TournamentId,
                tournament.Settings.PointsForWin,
                tournament.Settings.PointsForDraw,
                tournament.Settings.PointsForLoss,
                tournament.Settings.MatchDurationMinutes,
                tournament.Settings.BreakDurationMinutes,
                tournament.Settings.IsThirdPlaceMatchEnabled,
                tournament.Settings.RefereeEditWindowMinutes,
                tournament.Settings.WalkoverHomeScore,
                tournament.Settings.WalkoverAwayScore,
                tournament.Settings.CanCoachEditSquadAfterStart
            );

            return Result.Success(tournamentSettingsDto);
        }
    }
}

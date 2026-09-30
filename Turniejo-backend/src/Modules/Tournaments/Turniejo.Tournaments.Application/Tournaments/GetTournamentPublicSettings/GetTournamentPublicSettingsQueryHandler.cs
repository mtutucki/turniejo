using Turniejo.SharedKernel.Contracts;
using Turniejo.SharedKernel.CQRS;
using Turniejo.SharedKernel.Results;
using Turniejo.Tournaments.Application.Abstractions;

namespace Turniejo.Tournaments.Application.Tournaments.GetTournamentPublicSettings
{
    internal sealed class GetTournamentPublicSettingsQueryHandler(ITournamentsDbContext dbContext)
        : IQueryHandler<GetTournamentPublicSettingsQuery, TournamentPublicSettingsDto>
    {
        public async Task<Result<TournamentPublicSettingsDto>> Handle(GetTournamentPublicSettingsQuery request, CancellationToken cancellationToken)
        {
            var tournament = await dbContext.GetTournamentByIdAsync(request.TournamentId, cancellationToken);
            if (tournament == null)
                return Result.Failure<TournamentPublicSettingsDto>(Error.NotFound("Tournament", request.TournamentId));

            var s = tournament.PublicSettings;
            return Result.Success(new TournamentPublicSettingsDto(
                tournament.Id, s.IsPublic, s.ShowTeams, s.ShowPlayers, s.ShowSchedule,
                s.ShowStandings, s.ShowBracket, s.ShowLiveMatches, s.ShowStats,
                s.AbbreviatePlayerNames));
        }
    }
}

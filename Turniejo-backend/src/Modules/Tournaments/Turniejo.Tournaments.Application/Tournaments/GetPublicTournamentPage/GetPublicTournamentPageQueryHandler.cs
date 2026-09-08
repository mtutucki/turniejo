using Turniejo.SharedKernel.CQRS;
using Turniejo.SharedKernel.Results;
using Turniejo.Tournaments.Application.Abstractions;
using Turniejo.Tournaments.Application.DTOs;

namespace Turniejo.Tournaments.Application.Tournaments.GetPublicTournamentPage
{
    internal sealed class GetPublicTournamentPageQueryHandler(ITournamentsDbContext dbContext)
        : IQueryHandler<GetPublicTournamentPageQuery, PublicTournamentPageDto>
    {
        public async Task<Result<PublicTournamentPageDto>> Handle(GetPublicTournamentPageQuery request, CancellationToken cancellationToken)
        {
            var tournament = await dbContext.GetTournamentBySlugAsync(request.Slug, cancellationToken);
            if (tournament == null || !tournament.PublicSettings.IsPublic)
                return Result.Failure<PublicTournamentPageDto>(Error.NotFound("Tournament", request.Slug));

            var settings = new PublicTournamentSettingsDto(
                tournament.PublicSettings.ShowTeams, tournament.PublicSettings.ShowPlayers,
                tournament.PublicSettings.ShowSchedule, tournament.PublicSettings.ShowStandings,
                tournament.PublicSettings.ShowBracket, tournament.PublicSettings.ShowLiveMatches);

            var dto = new PublicTournamentPageDto(
                tournament.Id, tournament.Name, tournament.Slug, tournament.Status.ToString(), tournament.Format?.ToString(),
                tournament.StartAt, tournament.EndAt, tournament.Description, settings);

            return Result.Success(dto);
        }
    }
}

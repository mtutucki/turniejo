using Turniejo.SharedKernel.Contracts;
using Turniejo.SharedKernel.CQRS;
using Turniejo.SharedKernel.Results;
using Turniejo.Tournaments.Application.Abstractions;
using Turniejo.Tournaments.Application.DTOs;

namespace Turniejo.Tournaments.Application.Tournaments.GetPublicTournamentPage
{
    internal sealed class GetPublicTournamentPageQueryHandler(ITournamentsDbContext dbContext, IMatchModule matchModule)
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
                tournament.PublicSettings.ShowBracket, tournament.PublicSettings.ShowLiveMatches,
                tournament.PublicSettings.ShowStats);

            var categories = (await dbContext.GetTournamentCategoriesByTournamentIdAsync(tournament.Id, cancellationToken))
                .OrderBy(c => c.Order).ThenBy(c => c.Name)
                .Select(c => new PublicCategoryDto(c.Id, c.Name, c.Order))
                .ToList();
            var groups = (await dbContext.GetTournamentGroupsByTournamentIdAsync(tournament.Id, cancellationToken))
                .OrderBy(g => g.Order).ThenBy(g => g.Name)
                .Select(g => new PublicGroupDto(g.Id, g.CategoryId, g.Name, g.Order))
                .ToList();
            var pitches = (await matchModule.GetPitchesByTournamentIdAsync(tournament.Id, cancellationToken))
                .OrderBy(p => p.Name)
                .Select(p => new PublicPitchDto(p.Id, p.Name))
                .ToList();

            var dto = new PublicTournamentPageDto(
                tournament.Id, tournament.Name, tournament.Slug, tournament.Status.ToString(), tournament.Format?.ToString(),
                tournament.StartAt, tournament.EndAt, tournament.Description, settings, categories, groups, pitches);

            return Result.Success(dto);
        }
    }
}

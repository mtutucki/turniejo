using Turniejo.SharedKernel.Contracts;
using Turniejo.SharedKernel.CQRS;
using Turniejo.SharedKernel.Results;
using Turniejo.Tournaments.Application.Abstractions;
using Turniejo.Tournaments.Application.DTOs;

namespace Turniejo.Tournaments.Application.Tournaments.GetPublicTournaments
{
    internal sealed class GetPublicTournamentsQueryHandler(
        ITournamentsDbContext dbContext,
        IOrganizationModule organizationModule,
        ITeamModule teamModule,
        IMatchModule matchModule)
        : IQueryHandler<GetPublicTournamentsQuery, IReadOnlyList<PublicTournamentListItemDto>>
    {
        public async Task<Result<IReadOnlyList<PublicTournamentListItemDto>>> Handle(GetPublicTournamentsQuery request, CancellationToken cancellationToken)
        {
            // Szkice i odwołane nie trafiają na listę, nawet z włączonym portalem (decyzja 2026-11-10).
            var tournaments = await dbContext.GetListedPublicTournamentsAsync(cancellationToken);
            if (tournaments.Count == 0)
                return Result.Success<IReadOnlyList<PublicTournamentListItemDto>>([]);

            var tournamentIds = tournaments.Select(t => t.Id).ToList();
            var organizationIds = tournaments.Select(t => t.OrganizationId).Distinct().ToList();

            var organizations = await organizationModule.GetOrganizationNamesAsync(organizationIds, cancellationToken);
            var teamCounts = await teamModule.CountAcceptedTeamsByTournamentIdsAsync(tournamentIds, cancellationToken);
            var liveCounts = await matchModule.CountLiveMatchesByTournamentIdsAsync(tournamentIds, cancellationToken);
            var categories = (await dbContext.GetTournamentCategoriesByTournamentIdsAsync(tournamentIds, cancellationToken))
                .GroupBy(c => c.TournamentId)
                .ToDictionary(g => g.Key, g => (IReadOnlyList<string>)g.OrderBy(c => c.Order).ThenBy(c => c.Name).Select(c => c.Name).ToList());

            var items = tournaments
                .Select(t => new PublicTournamentListItemDto(
                    t.Name, t.Slug, t.Status.ToString(), t.StartAt, t.EndAt,
                    new PublicOrganizerDto(t.OrganizationId, organizations.GetValueOrDefault(t.OrganizationId, "")),
                    categories.GetValueOrDefault(t.Id, []),
                    teamCounts.GetValueOrDefault(t.Id),
                    liveCounts.GetValueOrDefault(t.Id)))
                .OrderBy(t => t.StartAt).ThenBy(t => t.Name)
                .ToList();

            return Result.Success<IReadOnlyList<PublicTournamentListItemDto>>(items);
        }
    }
}

using Turniejo.SharedKernel.CQRS;
using Turniejo.SharedKernel.Results;
using Turniejo.Tournaments.Application.Abstractions;
using Turniejo.Tournaments.Application.DTOs;

namespace Turniejo.Tournaments.Application.TournamentGroups.GetTournamentGroups
{
    internal sealed class GetTournamentGroupsQueryHandler(ITournamentsDbContext dbContext) : IQueryHandler<GetTournamentGroupsQuery, IReadOnlyList<TournamentGroupDto>>
    {
        public async Task<Result<IReadOnlyList<TournamentGroupDto>>> Handle(GetTournamentGroupsQuery request, CancellationToken cancellationToken)
        {
            var groups = await dbContext.GetTournamentGroupsByTournamentIdAsync(request.TournamentId, cancellationToken);
            var dtos = groups.Select(c => new TournamentGroupDto(
                c.Id, c.TournamentId, c.CategoryId, c.Name, c.Order, c.Stage)).ToList();

            return Result.Success<IReadOnlyList<TournamentGroupDto>>(dtos);
        }
    }
}

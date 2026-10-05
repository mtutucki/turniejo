using Turniejo.SharedKernel.CQRS;
using Turniejo.SharedKernel.Results;
using Turniejo.Tournaments.Application.Abstractions;
using Turniejo.Tournaments.Application.DTOs;

namespace Turniejo.Tournaments.Application.TieBreakerRules.GetTieBreakerRules
{
    internal sealed class GetTieBreakerRulesQueryHandler(ITournamentsDbContext dbContext) : IQueryHandler<GetTieBreakerRulesQuery, IReadOnlyList<TieBreakerRuleDto>>
    {
        public async Task<Result<IReadOnlyList<TieBreakerRuleDto>>> Handle(GetTieBreakerRulesQuery request, CancellationToken cancellationToken)
        {
            var rules = await dbContext.GetTieBreakerRulesByTournamentIdAsync(request.TournamentId, cancellationToken);

            var dtos = rules
                .OrderBy(r => r.Order)
                .Select(r => new TieBreakerRuleDto(r.Order, r.Type.ToString()))
                .ToList();

            return Result.Success<IReadOnlyList<TieBreakerRuleDto>>(dtos);
        }
    }
}

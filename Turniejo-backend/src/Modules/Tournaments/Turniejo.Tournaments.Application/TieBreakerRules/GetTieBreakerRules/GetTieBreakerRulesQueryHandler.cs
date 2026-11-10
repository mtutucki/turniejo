using Turniejo.SharedKernel.CQRS;
using Turniejo.SharedKernel.Results;
using Turniejo.Tournaments.Application.Abstractions;
using Turniejo.Tournaments.Application.DTOs;
using Turniejo.Tournaments.Domain.Entities;

namespace Turniejo.Tournaments.Application.TieBreakerRules.GetTieBreakerRules
{
    internal sealed class GetTieBreakerRulesQueryHandler(ITournamentsDbContext dbContext) : IQueryHandler<GetTieBreakerRulesQuery, IReadOnlyList<TieBreakerRuleDto>>
    {
        public async Task<Result<IReadOnlyList<TieBreakerRuleDto>>> Handle(GetTieBreakerRulesQuery request, CancellationToken cancellationToken)
        {
            var rules = await dbContext.GetTieBreakerRulesByTournamentIdAsync(request.TournamentId, cancellationToken);

            // Zapis pustej listy jest zablokowany, więc brak reguł = organizator nic nie ustawił.
            var dtos = rules.Count == 0
                ? TieBreakerRule.DefaultOrder.Select((type, i) => new TieBreakerRuleDto(i + 1, type.ToString())).ToList()
                : rules
                    .OrderBy(r => r.Order)
                    .Select(r => new TieBreakerRuleDto(r.Order, r.Type.ToString()))
                    .ToList();

            return Result.Success<IReadOnlyList<TieBreakerRuleDto>>(dtos);
        }
    }
}

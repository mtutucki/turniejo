using System.Text.Json;
using Turniejo.Brackets.Application.Abstractions;
using Turniejo.Brackets.Application.Services;
using Turniejo.Brackets.Domain.Enums;
using Turniejo.Brackets.Domain.Events;
using Turniejo.SharedKernel.CQRS;
using Turniejo.SharedKernel.Outbox;
using Turniejo.SharedKernel.Results;

namespace Turniejo.Brackets.Application.Brackets.ResolveGroupPositionSlots
{
    // Domyka przepływ z arkusza: drabinka może powstać (puste sloty GroupPosition) zanim faza grupowa
    // się skończy. Bez tego handlera te sloty nigdy same się nie rozwiązują — ktoś musiałby ręcznie
    // odświeżyć drabinkę po fakcie. Wołane przez StandingUpdatedHandler po każdej zmianie tabeli grupy.
    internal sealed class ResolveGroupPositionSlotsCommandHandler(
        IBracketsDbContext dbContext,
        BracketAdvancer bracketAdvancer) : ICommandHandler<ResolveGroupPositionSlotsCommand, ResolveGroupPositionSlotsResponse>
    {
        public async Task<Result<ResolveGroupPositionSlotsResponse>> Handle(ResolveGroupPositionSlotsCommand request, CancellationToken cancellationToken)
        {
            var bracket = await dbContext.GetBracketByTournamentAndCategoryAsync(request.TournamentId, request.CategoryId, cancellationToken);
            if (bracket == null)
                return Result.Success(new ResolveGroupPositionSlotsResponse(false));

            var rounds = await dbContext.GetBracketRoundsByBracketIdAsync(bracket.Id, cancellationToken);
            var nodeIds = (await dbContext.GetBracketNodesByRoundIdsAsync(rounds.Select(r => r.Id).ToList(), cancellationToken))
                .Select(n => n.Id)
                .ToList();
            // GetBracketNodesByRoundIdsAsync jest tylko do odczytu (AsNoTracking) — obsadzone sloty i MatchId
            // nie trafiały do bazy, więc każda kolejna zmiana tabeli tworzyła mecze tych samych par od nowa.
            var nodes = await dbContext.GetTrackedBracketNodesByIdsAsync(nodeIds, cancellationToken);

            var resolvedAny = false;

            // Najpierw sloty już obsadzone - korekta wyniku mogła zmienić kolejność w tabeli.
            var blockedMatchIds = new List<Guid>();
            foreach (var node in nodes)
                resolvedAny |= await bracketAdvancer.RefreshResolvedGroupSlotsAsync(
                    node, request.GroupId, request.TournamentId, request.CategoryId, blockedMatchIds, cancellationToken);

            foreach (var node in nodes)
            {
                var referencesThisGroup =
                    (node.HomeSourceType == BracketSourceType.GroupPosition && node.HomeSourceGroupId == request.GroupId && !node.HomeTournamentTeamId.HasValue) ||
                    (node.AwaySourceType == BracketSourceType.GroupPosition && node.AwaySourceGroupId == request.GroupId && !node.AwayTournamentTeamId.HasValue);

                if (!referencesThisGroup)
                    continue;

                var wasResolved = node.IsFullyResolved;
                await bracketAdvancer.ResolveGroupPositionSourcesAsync(node, cancellationToken);

                if (node.IsFullyResolved)
                {
                    if (!wasResolved)
                    {
                        resolvedAny = true;
                        var stageType = node.WinnerGoesToNodeId.HasValue ? "Knockout" : "Final";
                        if (rounds.First(r => r.Id == node.BracketRoundId).RoundType == BracketRoundType.ThirdPlace)
                            stageType = "ThirdPlace";

                        await bracketAdvancer.TryCreateMatchAsync(node, request.TournamentId, request.CategoryId, stageType, cancellationToken);
                    }
                }
                else if (await bracketAdvancer.TryAdvanceByeAsync(node, request.TournamentId, request.CategoryId, cancellationToken))
                {
                    // Węzeł ma Bye z jednej strony — druga strona (właśnie rozwiązana GroupPosition)
                    // awansuje bez meczu, więc IsFullyResolved tego węzła NIGDY nie będzie true.
                    resolvedAny = true;
                }
            }

            if (resolvedAny)
            {
                var domainEvent = new TeamsAdvancedToBracket(bracket.Id, bracket.TournamentId);
                var outboxMessage = OutboxMessage.Create(
                    domainEvent.GetType().AssemblyQualifiedName!,
                    JsonSerializer.Serialize(domainEvent, domainEvent.GetType()));

                await dbContext.AddOutboxMessageAsync(outboxMessage, cancellationToken);
                await dbContext.SaveChangesAsync(cancellationToken);
            }

            return Result.Success(new ResolveGroupPositionSlotsResponse(resolvedAny));
        }
    }
}

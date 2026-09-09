using System.Text.Json;
using Turniejo.Brackets.Application.Abstractions;
using Turniejo.Brackets.Application.Services;
using Turniejo.Brackets.Domain.Entities;
using Turniejo.Brackets.Domain.Enums;
using Turniejo.Brackets.Domain.Events;
using Turniejo.SharedKernel.Contracts;
using Turniejo.SharedKernel.CQRS;
using Turniejo.SharedKernel.Outbox;
using Turniejo.SharedKernel.Results;

namespace Turniejo.Brackets.Application.Brackets.GenerateBracket
{
    internal sealed class GenerateBracketCommandHandler(
        IBracketsDbContext dbContext,
        ITournamentModule tournamentModule,
        BracketAdvancer bracketAdvancer) : ICommandHandler<GenerateBracketCommand, GenerateBracketResponse>
    {
        private sealed record Advancement(Guid? WinnerNodeId, BracketSlot? WinnerSlot, Guid? LoserNodeId, BracketSlot? LoserSlot);

        public async Task<Result<GenerateBracketResponse>> Handle(GenerateBracketCommand request, CancellationToken cancellationToken)
        {
            var tournament = await tournamentModule.GetTournamentAsync(request.TournamentId, cancellationToken);
            if (tournament == null)
                return Result.Failure<GenerateBracketResponse>(Error.NotFound("Tournament", request.TournamentId));

            if (tournament.Format is not ("Knockout" or "GroupAndKnockout"))
                return Result.Failure<GenerateBracketResponse>(Error.Validation("Format", "Bracket generation requires Knockout or GroupAndKnockout format"));

            var existingBracket = await dbContext.GetBracketByTournamentAndCategoryAsync(request.TournamentId, request.CategoryId, cancellationToken);
            if (existingBracket != null)
                return Result.Failure<GenerateBracketResponse>(Error.Conflict("Bracket", "A bracket already exists for this tournament"));

            var settings = await tournamentModule.GetTournamentSettingsAsync(request.TournamentId, cancellationToken);
            if (settings == null)
                return Result.Failure<GenerateBracketResponse>(Error.NotFound("TournamentSettings", request.TournamentId));

            var bracketResult = Bracket.Create(Guid.NewGuid(), request.TournamentId, request.CategoryId);
            if (bracketResult.IsFailure)
                return Result.Failure<GenerateBracketResponse>(bracketResult.Error);

            var bracket = bracketResult.Value;
            await dbContext.AddBracketAsync(bracket, cancellationToken);

            var totalRounds = (int)Math.Log2(request.RoundOnePairings.Count) + 1;

            // WinnerNodeId/LoserNodeId a node should advance into, resolved as the tree is built,
            // then applied once per node at the end (WireAdvancement sets all four fields together).
            var advancements = new Dictionary<Guid, Advancement>();
            var nodesById = new Dictionary<Guid, BracketNode>();

            // Round 1: sourced directly from the organizer-supplied pairings.
            var round1Result = BracketRound.Create(Guid.NewGuid(), bracket.Id, 1, BracketRoundType.Standard, "Round 1");
            if (round1Result.IsFailure)
                return Result.Failure<GenerateBracketResponse>(round1Result.Error);

            await dbContext.AddBracketRoundAsync(round1Result.Value, cancellationToken);

            var previousRoundNodes = new List<BracketNode>();
            foreach (var pairing in request.RoundOnePairings)
            {
                var nodeResult = BracketNode.Create(
                    Guid.NewGuid(), round1Result.Value.Id,
                    pairing.HomeSourceType, pairing.HomeGroupId, pairing.HomePosition, null, pairing.HomeTournamentTeamId,
                    pairing.AwaySourceType, pairing.AwayGroupId, pairing.AwayPosition, null, pairing.AwayTournamentTeamId);
                if (nodeResult.IsFailure)
                    return Result.Failure<GenerateBracketResponse>(nodeResult.Error);

                await dbContext.AddBracketNodeAsync(nodeResult.Value, cancellationToken);
                nodesById[nodeResult.Value.Id] = nodeResult.Value;
                previousRoundNodes.Add(nodeResult.Value);
            }

            // Rounds 2..N: each node's two slots are filled by the winners of the two nodes that feed it.
            var semifinalNodes = new List<BracketNode>();
            for (var roundNumber = 2; roundNumber <= totalRounds; roundNumber++)
            {
                var roundName = roundNumber == totalRounds ? "Final" : $"Round {roundNumber}";
                var roundResult = BracketRound.Create(Guid.NewGuid(), bracket.Id, roundNumber, BracketRoundType.Standard, roundName);
                if (roundResult.IsFailure)
                    return Result.Failure<GenerateBracketResponse>(roundResult.Error);

                await dbContext.AddBracketRoundAsync(roundResult.Value, cancellationToken);

                if (roundNumber == totalRounds)
                    semifinalNodes = previousRoundNodes;

                var currentRoundNodes = new List<BracketNode>();
                for (var i = 0; i < previousRoundNodes.Count; i += 2)
                {
                    var homeSource = previousRoundNodes[i];
                    var awaySource = previousRoundNodes[i + 1];

                    var nodeResult = BracketNode.Create(
                        Guid.NewGuid(), roundResult.Value.Id,
                        BracketSourceType.MatchWinner, null, null, homeSource.Id, null,
                        BracketSourceType.MatchWinner, null, null, awaySource.Id, null);
                    if (nodeResult.IsFailure)
                        return Result.Failure<GenerateBracketResponse>(nodeResult.Error);

                    await dbContext.AddBracketNodeAsync(nodeResult.Value, cancellationToken);
                    nodesById[nodeResult.Value.Id] = nodeResult.Value;
                    currentRoundNodes.Add(nodeResult.Value);

                    advancements[homeSource.Id] = WithWinner(advancements, homeSource.Id, nodeResult.Value.Id, BracketSlot.Home);
                    advancements[awaySource.Id] = WithWinner(advancements, awaySource.Id, nodeResult.Value.Id, BracketSlot.Away);
                }

                previousRoundNodes = currentRoundNodes;
            }

            // Optional 3rd place match, sourced from the losers of the two semifinals.
            if (settings.IsThirdPlaceMatchEnabled && semifinalNodes.Count == 2)
            {
                var thirdPlaceRoundResult = BracketRound.Create(Guid.NewGuid(), bracket.Id, totalRounds, BracketRoundType.ThirdPlace, "3rd Place");
                if (thirdPlaceRoundResult.IsFailure)
                    return Result.Failure<GenerateBracketResponse>(thirdPlaceRoundResult.Error);

                await dbContext.AddBracketRoundAsync(thirdPlaceRoundResult.Value, cancellationToken);

                var thirdPlaceNodeResult = BracketNode.Create(
                    Guid.NewGuid(), thirdPlaceRoundResult.Value.Id,
                    BracketSourceType.MatchLoser, null, null, semifinalNodes[0].Id, null,
                    BracketSourceType.MatchLoser, null, null, semifinalNodes[1].Id, null);
                if (thirdPlaceNodeResult.IsFailure)
                    return Result.Failure<GenerateBracketResponse>(thirdPlaceNodeResult.Error);

                await dbContext.AddBracketNodeAsync(thirdPlaceNodeResult.Value, cancellationToken);
                nodesById[thirdPlaceNodeResult.Value.Id] = thirdPlaceNodeResult.Value;

                advancements[semifinalNodes[0].Id] = advancements[semifinalNodes[0].Id]
                    with { LoserNodeId = thirdPlaceNodeResult.Value.Id, LoserSlot = BracketSlot.Home };
                advancements[semifinalNodes[1].Id] = advancements[semifinalNodes[1].Id]
                    with { LoserNodeId = thirdPlaceNodeResult.Value.Id, LoserSlot = BracketSlot.Away };
            }

            // Zapis pośredni PRZED wpięciem WinnerGoesToNodeId/LoserGoesToNodeId: węzeł Rundy 1 wskazuje
            // na węzeł Rundy 2 (WinnerGoesToNodeId), a węzeł Rundy 2 już wskazuje na węzeł Rundy 1
            // (HomeSourceNodeId/AwaySourceNodeId, ustawione przy tworzeniu) — dwa nowe, wzajemnie
            // powiązane wiersze w JEDNYM SaveChanges to cykl w grafie FK, którego EF Core nie potrafi
            // topologicznie posortować (nawet przy DeleteBehavior.SetNull na obu końcach), więc rzuca
            // "circular dependency was detected". Zapisanie węzłów najpierw BEZ tych pól (same wiszą
            // "w przód": Runda N -> Runda N-1) usuwa cykl; doklejenie WinnerGoesToNodeId/LoserGoesToNodeId
            // potem to już zwykły UPDATE na istniejących wierszach.
            await dbContext.SaveChangesAsync(cancellationToken);

            foreach (var (nodeId, advancement) in advancements)
            {
                nodesById[nodeId].WireAdvancement(advancement.WinnerNodeId, advancement.WinnerSlot, advancement.LoserNodeId, advancement.LoserSlot);
            }

            // Resolve any GroupPosition sources from final group standings, then create matches for every fully-resolved node.
            var resolvedAnyGroupPosition = false;
            foreach (var node in nodesById.Values)
            {
                if (node.HomeSourceType == BracketSourceType.GroupPosition || node.AwaySourceType == BracketSourceType.GroupPosition)
                {
                    var wasResolved = node.IsFullyResolved;
                    await bracketAdvancer.ResolveGroupPositionSourcesAsync(node, cancellationToken);
                    if (!wasResolved && node.IsFullyResolved)
                        resolvedAnyGroupPosition = true;
                }
            }

            // Bye: węzeł Rundy 1 z jedną stroną Bye nigdy nie dostaje meczu (druga strona sama nie
            // wystarcza do IsFullyResolved), więc trzeba ręcznie wepchnąć drużynę dalej — inaczej drabinka
            // zostaje z permanentnie pustym slotem w Rundzie 2, mimo że nikt nie musiał grać.
            foreach (var node in nodesById.Values.Where(n => n.BracketRoundId == round1Result.Value.Id).ToList())
            {
                await bracketAdvancer.TryAdvanceByeAsync(node, request.TournamentId, request.CategoryId, cancellationToken);
            }

            var round1StageType = totalRounds == 1 ? "Final" : "Knockout";
            foreach (var node in nodesById.Values.Where(n => n.BracketRoundId == round1Result.Value.Id))
            {
                var result = await bracketAdvancer.TryCreateMatchAsync(node, request.TournamentId, request.CategoryId, round1StageType, cancellationToken);
                if (result.IsFailure)
                    return Result.Failure<GenerateBracketResponse>(result.Error);
            }

            foreach (var domainEvent in bracket.DomainEvents)
                await AddOutboxEventAsync(domainEvent, cancellationToken);

            if (resolvedAnyGroupPosition)
                await AddOutboxEventAsync(new TeamsAdvancedToBracket(bracket.Id, bracket.TournamentId), cancellationToken);

            await dbContext.SaveChangesAsync(cancellationToken);

            return Result.Success(new GenerateBracketResponse(bracket.Id, totalRounds));
        }

        private static Advancement WithWinner(Dictionary<Guid, Advancement> advancements, Guid nodeId, Guid winnerNodeId, BracketSlot slot)
        {
            var current = advancements.TryGetValue(nodeId, out var existing) ? existing : new Advancement(null, null, null, null);
            return current with { WinnerNodeId = winnerNodeId, WinnerSlot = slot };
        }

        private async Task AddOutboxEventAsync(object domainEvent, CancellationToken cancellationToken)
        {
            var outboxMessage = OutboxMessage.Create(
                domainEvent.GetType().AssemblyQualifiedName!,
                JsonSerializer.Serialize(domainEvent, domainEvent.GetType()));

            await dbContext.AddOutboxMessageAsync(outboxMessage, cancellationToken);
        }
    }
}

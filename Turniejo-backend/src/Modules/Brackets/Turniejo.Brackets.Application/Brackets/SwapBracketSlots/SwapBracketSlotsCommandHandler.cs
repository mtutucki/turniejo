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

namespace Turniejo.Brackets.Application.Brackets.SwapBracketSlots
{
    internal sealed class SwapBracketSlotsCommandHandler(
        IBracketsDbContext dbContext,
        IMatchModule matchModule,
        BracketAdvancer bracketAdvancer) : ICommandHandler<SwapBracketSlotsCommand>
    {
        private static readonly Error MatchStarted = Error.Conflict(
            "Bracket", "Cannot change the pairings once a bracket match has started, finished or been cancelled");

        public async Task<Result> Handle(SwapBracketSlotsCommand request, CancellationToken cancellationToken)
        {
            if (request.FirstNodeId == request.SecondNodeId && request.FirstSlot == request.SecondSlot)
                return Result.Failure(Error.Validation("Slot", "Choose two different slots to swap"));

            var bracket = await dbContext.GetBracketByTournamentAndCategoryAsync(request.TournamentId, request.CategoryId, cancellationToken);
            if (bracket == null)
                return Result.Failure(Error.NotFound("Bracket", request.TournamentId));

            var rounds = await dbContext.GetBracketRoundsByBracketIdAsync(bracket.Id, cancellationToken);
            var allNodes = await dbContext.GetBracketNodesByRoundIdsAsync(rounds.Select(r => r.Id).ToList(), cancellationToken);

            var roundOneId = rounds.FirstOrDefault(r => r.RoundType == BracketRoundType.Standard && r.RoundNumber == 1)?.Id;
            var swappedIds = new[] { request.FirstNodeId, request.SecondNodeId }.Distinct().ToList();
            if (swappedIds.Any(id => !allNodes.Any(n => n.Id == id && n.BracketRoundId == roundOneId)))
                return Result.Failure(Error.Validation("NodeId", "Only first-round slots can be swapped"));

            // Dopóki nic nie zagrano, sloty kolejnych rund wypełniają wyłącznie wolne losy — to pozwala
            // po zamianie wyliczyć je od nowa. Po pierwszym meczu trzeba by cofać awanse i wyniki.
            var matchById = new Dictionary<Guid, MatchInfoDto>();
            foreach (var matchId in allNodes.Where(n => n.MatchId.HasValue).Select(n => n.MatchId!.Value))
            {
                var match = await matchModule.GetMatchAsync(matchId, cancellationToken);
                if (match == null)
                    continue;

                if (match.Status is not ("Scheduled" or "Ready" or "Postponed"))
                    return Result.Failure(MatchStarted);

                matchById[matchId] = match;
            }

            var targetIds = allNodes
                .Where(n => swappedIds.Contains(n.Id) && n.WinnerGoesToNodeId.HasValue)
                .Select(n => n.WinnerGoesToNodeId!.Value);
            var tracked = (await dbContext.GetTrackedBracketNodesByIdsAsync(swappedIds.Concat(targetIds).Distinct().ToList(), cancellationToken))
                .ToDictionary(n => n.Id);
            var first = tracked[request.FirstNodeId];
            var second = tracked[request.SecondNodeId];

            var firstSource = first.GetSlotSource(request.FirstSlot);
            var secondSource = second.GetSlotSource(request.SecondSlot);
            if (firstSource == secondSource)
                return Result.Success();

            if (first.Id != second.Id &&
                (WouldBeDoubleBye(first, request.FirstSlot, secondSource) || WouldBeDoubleBye(second, request.SecondSlot, firstSource)))
                return Result.Failure(Error.Validation("Bye", "A pairing cannot have Bye on both sides — there would be no team to advance."));

            first.SetSlotSource(request.FirstSlot, secondSource);
            second.SetSlotSource(request.SecondSlot, firstSource);

            var swapped = swappedIds.Select(id => tracked[id]).ToList();

            // Wolny los: drużyna z pary przechodzi dalej bez meczu, więc slot kolejnej rundy ma ją od razu.
            // Para bez wolnego losu zostawia ten slot pusty do rozegrania meczu.
            foreach (var node in swapped)
            {
                if (node.WinnerGoesToNodeId is not { } targetId || node.WinnerGoesToSlot is not { } targetSlot)
                    continue;

                var target = tracked[targetId];
                if (IsByeNode(node) && ByeAdvancingTeamId(node) is { } advancingTeamId)
                    target.ResolveSlot(targetSlot, advancingTeamId);
                else
                    target.ClearSlot(targetSlot);
            }

            var affected = tracked.Values.ToList();

            // Najpierw istniejące mecze: podmiana drużyn albo usunięcie, gdy para przestała być kompletna.
            // Termin usuniętego meczu zostaje na węźle — przejmie go mecz, który powstanie w tej rundzie.
            var freed = new List<BracketNode>();
            foreach (var node in affected)
            {
                if (node.MatchId is not { } matchId)
                    continue;

                if (!matchById.TryGetValue(matchId, out var match))
                {
                    node.ClearMatch();
                    continue;
                }

                if (node.IsFullyResolved)
                {
                    var home = node.HomeTournamentTeamId!.Value;
                    var away = node.AwayTournamentTeamId!.Value;
                    if ((match.HomeTournamentTeamId != home || match.AwayTournamentTeamId != away) &&
                        !await matchModule.ChangeUnstartedMatchTeamsAsync(matchId, home, away, cancellationToken))
                        return Result.Failure(MatchStarted);

                    continue;
                }

                if (!await matchModule.RemoveUnstartedMatchesAsync([matchId], cancellationToken))
                    return Result.Failure(MatchStarted);

                node.ClearMatch();
                if (match.PitchId is { } pitchId)
                {
                    node.PlanSlot(pitchId, match.ScheduledAt);
                    freed.Add(node);
                }
            }

            foreach (var node in affected.Where(n => n.IsFullyResolved && !n.MatchId.HasValue))
            {
                if (!node.PlannedAt.HasValue &&
                    freed.FirstOrDefault(f => f.Id != node.Id && f.BracketRoundId == node.BracketRoundId && f.PlannedAt.HasValue) is { } donor)
                {
                    node.PlanSlot(donor.PlannedPitchId!.Value, donor.PlannedAt!.Value);
                    donor.ClearPlannedSlot();
                }

                var stageType = await bracketAdvancer.ResolveStageTypeAsync(node, cancellationToken);
                var created = await bracketAdvancer.TryCreateMatchAsync(node, request.TournamentId, request.CategoryId, stageType, cancellationToken);
                if (created.IsFailure)
                    return created;
            }

            // Zapowiedź nie może zaczynać się razem z meczem, z którego przyjdzie do niej drużyna.
            var startByNode = new Dictionary<Guid, DateTimeOffset>();
            foreach (var node in affected.Where(n => n.MatchId.HasValue))
                if (await matchModule.GetMatchAsync(node.MatchId!.Value, cancellationToken) is { } nodeMatch)
                    startByNode[node.Id] = nodeMatch.ScheduledAt;
            PlannedSlotGuard.ClearPlansNotAfterSources(
                affected, n => startByNode.TryGetValue(n.Id, out var at) ? at : null);

            // Para z wolnym losem nigdy nie dostanie meczu — nie może wisieć w terminarzu jako zapowiedź.
            foreach (var node in swapped.Where(n => IsByeNode(n) && n.PlannedAt.HasValue))
                node.ClearPlannedSlot();

            var domainEvent = new BracketUpdated(bracket.Id, bracket.TournamentId);
            await dbContext.AddOutboxMessageAsync(
                OutboxMessage.Create(
                    domainEvent.GetType().AssemblyQualifiedName!,
                    JsonSerializer.Serialize(domainEvent, domainEvent.GetType())),
                cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);

            return Result.Success();
        }

        private static bool IsByeNode(BracketNode node) =>
            (node.HomeSourceType == BracketSourceType.Bye) != (node.AwaySourceType == BracketSourceType.Bye);

        private static Guid? ByeAdvancingTeamId(BracketNode node) =>
            node.HomeSourceType == BracketSourceType.Bye ? node.AwayTournamentTeamId : node.HomeTournamentTeamId;

        private static bool WouldBeDoubleBye(BracketNode node, BracketSlot replacedSlot, BracketSlotSource incoming)
        {
            var otherSlot = replacedSlot == BracketSlot.Home ? BracketSlot.Away : BracketSlot.Home;
            return incoming.SourceType == BracketSourceType.Bye && node.GetSlotSource(otherSlot).SourceType == BracketSourceType.Bye;
        }
    }
}

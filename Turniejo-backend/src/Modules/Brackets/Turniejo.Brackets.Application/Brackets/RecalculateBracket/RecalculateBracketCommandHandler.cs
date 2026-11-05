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

namespace Turniejo.Brackets.Application.Brackets.RecalculateBracket
{
    internal sealed class RecalculateBracketCommandHandler(
        IBracketsDbContext dbContext,
        IMatchModule matchModule,
        BracketAdvancer bracketAdvancer) : ICommandHandler<RecalculateBracketCommand, RecalculateBracketResponse>
    {
        public async Task<Result<RecalculateBracketResponse>> Handle(RecalculateBracketCommand request, CancellationToken cancellationToken)
        {
            var bracket = await dbContext.GetBracketByTournamentAndCategoryAsync(request.TournamentId, request.CategoryId, cancellationToken);
            if (bracket == null)
                return Result.Failure<RecalculateBracketResponse>(Error.NotFound("Bracket", request.TournamentId));

            // Rundy po kolei, mecz o 3. miejsce na końcu — awans z wcześniejszej rundy musi być gotowy,
            // zanim przeliczymy kolejną.
            var rounds = (await dbContext.GetBracketRoundsByBracketIdAsync(bracket.Id, cancellationToken))
                .OrderBy(r => r.RoundType == BracketRoundType.ThirdPlace)
                .ThenBy(r => r.RoundNumber)
                .ToList();
            var nodes = await dbContext.GetBracketNodesByRoundIdsAsync(rounds.Select(r => r.Id).ToList(), cancellationToken);
            var roundOrder = rounds.Select((r, i) => (r.Id, i)).ToDictionary(x => x.Id, x => x.i);

            var matchIds = nodes.Where(n => n.MatchId.HasValue).Select(n => n.MatchId!.Value).ToList();
            var winners = await matchModule.RefreshKnockoutWinnersAsync(matchIds, cancellationToken);

            var changed = false;
            var blocked = new List<Guid>();

            foreach (var snapshot in nodes.Where(n => n.MatchId.HasValue).OrderBy(n => roundOrder[n.BracketRoundId]))
            {
                if (!winners.TryGetValue(snapshot.MatchId!.Value, out var winnerId) || winnerId is not { } winner)
                    continue;

                // Śledzona instancja — po awansie z poprzedniej rundy ma już nowe drużyny.
                var node = await dbContext.GetBracketNodeByIdAsync(snapshot.Id, cancellationToken);
                if (node == null)
                    continue;

                changed |= await bracketAdvancer.AdvanceFromNodeAsync(node, winner, bracket.TournamentId, bracket.CategoryId, cancellationToken);

                var loser = node.HomeTournamentTeamId == winner ? node.AwayTournamentTeamId : node.HomeTournamentTeamId;
                await CollectBlockedAsync(node.WinnerGoesToNodeId, node.WinnerGoesToSlot, winner, blocked, cancellationToken);
                if (loser.HasValue)
                    await CollectBlockedAsync(node.LoserGoesToNodeId, node.LoserGoesToSlot, loser.Value, blocked, cancellationToken);
            }

            if (changed)
            {
                var domainEvent = new BracketUpdated(bracket.Id, bracket.TournamentId);
                await dbContext.AddOutboxMessageAsync(
                    OutboxMessage.Create(
                        domainEvent.GetType().AssemblyQualifiedName!,
                        JsonSerializer.Serialize(domainEvent, domainEvent.GetType())),
                    cancellationToken);
                await dbContext.SaveChangesAsync(cancellationToken);
            }

            return Result.Success(new RecalculateBracketResponse(changed, blocked));
        }

        // Slot nadal ma inną drużynę niż powinien — kolejny mecz już się zaczął i nie dało się go poprawić.
        private async Task CollectBlockedAsync(
            Guid? targetNodeId, BracketSlot? slot, Guid expectedTeamId, List<Guid> blocked, CancellationToken cancellationToken)
        {
            if (targetNodeId is not { } id || slot is not { } targetSlot)
                return;

            var target = await dbContext.GetBracketNodeByIdAsync(id, cancellationToken);
            if (target?.MatchId is not { } matchId)
                return;

            var current = targetSlot == BracketSlot.Home ? target.HomeTournamentTeamId : target.AwayTournamentTeamId;
            if (current != expectedTeamId && !blocked.Contains(matchId))
                blocked.Add(matchId);
        }
    }
}

using System.Text.Json;
using Turniejo.Brackets.Application.Abstractions;
using Turniejo.Brackets.Application.Services;
using Turniejo.Brackets.Domain.Entities;
using Turniejo.Brackets.Domain.Enums;
using Turniejo.Brackets.Domain.Events;
using Turniejo.SharedKernel.CQRS;
using Turniejo.SharedKernel.Outbox;
using Turniejo.SharedKernel.Results;

namespace Turniejo.Brackets.Application.Brackets.AdvanceBracketWinner
{
    internal sealed class AdvanceBracketWinnerCommandHandler(
        IBracketsDbContext dbContext,
        BracketAdvancer bracketAdvancer) : ICommandHandler<AdvanceBracketWinnerCommand, AdvanceBracketWinnerResponse>
    {
        public async Task<Result<AdvanceBracketWinnerResponse>> Handle(AdvanceBracketWinnerCommand request, CancellationToken cancellationToken)
        {
            var node = await dbContext.GetBracketNodeByMatchIdAsync(request.MatchId, cancellationToken);
            if (node == null || !request.WinnerTournamentTeamId.HasValue)
                return Result.Success(new AdvanceBracketWinnerResponse(false));

            var winnerId = request.WinnerTournamentTeamId.Value;
            Guid? loserId = null;
            if (node.HomeTournamentTeamId == winnerId)
                loserId = node.AwayTournamentTeamId;
            else if (node.AwayTournamentTeamId == winnerId)
                loserId = node.HomeTournamentTeamId;

            var round = await dbContext.GetBracketRoundByIdAsync(node.BracketRoundId, cancellationToken);
            if (round == null)
                return Result.Success(new AdvanceBracketWinnerResponse(false));

            var bracket = await dbContext.GetBracketByIdAsync(round.BracketId, cancellationToken);
            if (bracket == null)
                return Result.Success(new AdvanceBracketWinnerResponse(false));

            var advanced = false;

            if (node.WinnerGoesToNodeId.HasValue && node.WinnerGoesToSlot.HasValue)
            {
                advanced |= await AdvanceIntoAsync(
                    node.WinnerGoesToNodeId.Value, node.WinnerGoesToSlot.Value, winnerId, bracket, cancellationToken);
            }

            if (node.LoserGoesToNodeId.HasValue && node.LoserGoesToSlot.HasValue && loserId.HasValue)
            {
                advanced |= await AdvanceIntoAsync(
                    node.LoserGoesToNodeId.Value, node.LoserGoesToSlot.Value, loserId.Value, bracket, cancellationToken);
            }

            if (advanced)
            {
                var domainEvent = new BracketUpdated(bracket.Id, bracket.TournamentId);
                var outboxMessage = OutboxMessage.Create(
                    domainEvent.GetType().AssemblyQualifiedName!,
                    JsonSerializer.Serialize(domainEvent, domainEvent.GetType()));

                await dbContext.AddOutboxMessageAsync(outboxMessage, cancellationToken);
                await dbContext.SaveChangesAsync(cancellationToken);
            }

            return Result.Success(new AdvanceBracketWinnerResponse(advanced));
        }

        private async Task<bool> AdvanceIntoAsync(
            Guid targetNodeId, BracketSlot slot, Guid tournamentTeamId, Bracket bracket, CancellationToken cancellationToken)
        {
            var targetNode = await dbContext.GetBracketNodeByIdAsync(targetNodeId, cancellationToken);
            if (targetNode == null)
                return false;

            var resolveResult = targetNode.ResolveSlot(slot, tournamentTeamId);
            if (resolveResult.IsFailure)
                return false;

            var stageType = targetNode.WinnerGoesToNodeId.HasValue ? "Knockout" : "Final";
            var round = await dbContext.GetBracketRoundByIdAsync(targetNode.BracketRoundId, cancellationToken);
            if (round?.RoundType == BracketRoundType.ThirdPlace)
                stageType = "ThirdPlace";

            await bracketAdvancer.TryCreateMatchAsync(targetNode, bracket.TournamentId, bracket.CategoryId, stageType, cancellationToken);

            return true;
        }
    }
}

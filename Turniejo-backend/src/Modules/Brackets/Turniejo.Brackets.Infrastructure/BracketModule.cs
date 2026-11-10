using Turniejo.Brackets.Application.Abstractions;
using Turniejo.Brackets.Domain.Entities;
using Turniejo.Brackets.Domain.Enums;
using Turniejo.SharedKernel.Contracts;

namespace Turniejo.Brackets.Infrastructure;

internal sealed class BracketModule(IBracketsDbContext dbContext) : IBracketModule
{
    public async Task<IReadOnlyList<BracketNodeInfoDto>> GetNodesByTournamentIdAsync(Guid tournamentId, CancellationToken cancellationToken = default)
    {
        var brackets = await dbContext.GetBracketsByTournamentIdAsync(tournamentId, cancellationToken);
        var categoryByBracketId = brackets.ToDictionary(b => b.Id, b => b.CategoryId);
        var rounds = await GetRoundsAsync(tournamentId, cancellationToken);
        var roundById = rounds.ToDictionary(r => r.Id);
        var nodes = await dbContext.GetBracketNodesByRoundIdsAsync(roundById.Keys.ToList(), cancellationToken);

        return nodes
            .Select(n => new BracketNodeInfoDto(
                n.Id,
                roundById[n.BracketRoundId].RoundNumber,
                roundById[n.BracketRoundId].RoundType == BracketRoundType.ThirdPlace,
                n.MatchId, n.HomeSourceNodeId, n.AwaySourceNodeId,
                n.HomeSourceType == BracketSourceType.Bye || n.AwaySourceType == BracketSourceType.Bye,
                n.PlannedPitchId, n.PlannedAt, n.PlannedRefereeId,
                categoryByBracketId[roundById[n.BracketRoundId].BracketId],
                n.HomeSourceGroupId, n.AwaySourceGroupId,
                n.HomeTournamentTeamId, n.AwayTournamentTeamId))
            .ToList();
    }

    public async Task SetPlannedSlotsAsync(IReadOnlyCollection<BracketPlannedSlotDto> slots, CancellationToken cancellationToken = default)
    {
        if (slots.Count == 0)
            return;

        var slotByNodeId = slots.ToDictionary(s => s.NodeId);
        var nodes = await dbContext.GetTrackedBracketNodesByIdsAsync(slotByNodeId.Keys.ToList(), cancellationToken);
        foreach (var node in nodes)
            node.PlanSlot(slotByNodeId[node.Id].PitchId, slotByNodeId[node.Id].ScheduledAt);

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task ClearPlannedSlotsAsync(Guid tournamentId, CancellationToken cancellationToken = default)
    {
        var rounds = await GetRoundsAsync(tournamentId, cancellationToken);
        var nodeIds = (await dbContext.GetBracketNodesByRoundIdsAsync(rounds.Select(r => r.Id).ToList(), cancellationToken))
            .Where(n => n.PlannedAt.HasValue)
            .Select(n => n.Id)
            .ToList();
        if (nodeIds.Count == 0)
            return;

        foreach (var node in await dbContext.GetTrackedBracketNodesByIdsAsync(nodeIds, cancellationToken))
            node.ClearPlannedSlot();

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task SetPlannedRefereeAsync(Guid nodeId, Guid? refereeId, CancellationToken cancellationToken = default)
    {
        foreach (var node in await dbContext.GetTrackedBracketNodesByIdsAsync([nodeId], cancellationToken))
            node.PlanReferee(refereeId);

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<SecondStageSeatInfoDto>> GetSecondStageSeatsAsync(Guid tournamentId, CancellationToken cancellationToken = default) =>
        (await dbContext.GetSecondStageSeatsAsync(tournamentId, cancellationToken))
            .Select(s => new SecondStageSeatInfoDto(s.Id, s.CategoryId, s.GroupId, s.Order, s.SourceGroupId, s.SourcePosition, s.TournamentTeamId))
            .ToList();

    public async Task RemoveSecondStageSeatsForGroupAsync(Guid groupId, CancellationToken cancellationToken = default)
    {
        var seats = await dbContext.GetTrackedSecondStageSeatsByGroupAsync(groupId, cancellationToken);
        if (seats.Count == 0)
            return;

        dbContext.RemoveSecondStageSeats(seats);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task<List<BracketRound>> GetRoundsAsync(Guid tournamentId, CancellationToken cancellationToken)
    {
        var rounds = new List<BracketRound>();
        foreach (var bracket in await dbContext.GetBracketsByTournamentIdAsync(tournamentId, cancellationToken))
            rounds.AddRange(await dbContext.GetBracketRoundsByBracketIdAsync(bracket.Id, cancellationToken));

        return rounds;
    }
}

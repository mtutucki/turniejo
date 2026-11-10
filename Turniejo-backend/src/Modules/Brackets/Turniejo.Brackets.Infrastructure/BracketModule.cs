using Turniejo.Brackets.Application.Abstractions;
using Turniejo.Brackets.Application.SecondStage;
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
        await ClearSecondStageAsync(tournamentId, cancellationToken);

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
        var ranges = await dbContext.GetTrackedSecondStageRangesByGroupAsync(groupId, cancellationToken);
        if (seats.Count == 0 && ranges.Count == 0)
            return;

        dbContext.RemoveSecondStageSeats(seats);
        dbContext.RemoveSecondStageRanges(ranges);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task PrepareSecondStageSeatsAsync(
        Guid tournamentId, IReadOnlyList<TournamentGroupDto> groups, IReadOnlyDictionary<Guid, int> teamsInGroup,
        CancellationToken cancellationToken = default)
    {
        var allRanges = await dbContext.GetTrackedSecondStageRangesAsync(tournamentId, cancellationToken);
        var allSeats = await dbContext.GetTrackedSecondStageSeatsAsync(tournamentId, cancellationToken);
        var teams = (Guid groupId) => teamsInGroup.GetValueOrDefault(groupId);

        foreach (var category in groups.GroupBy(g => g.CategoryId))
        {
            var secondStage = category.Where(g => g.Stage == 2).Select(g => new SecondStagePlanner.Group(g.Id, g.Order)).ToList();
            var firstStage = category.Where(g => g.Stage != 2).Select(g => new SecondStagePlanner.Group(g.Id, g.Order)).ToList();
            if (secondStage.Count == 0)
                continue;

            var ranges = allRanges.Where(r => r.CategoryId == category.Key).ToList();
            var seats = allSeats.Where(s => s.CategoryId == category.Key).ToList();
            if (ranges.Count == 0 && seats.Count > 0)
                continue;

            if (ranges.Count == 0)
            {
                // Organizator nie ustawił podziału: gotowy podział po równo, zapisany jak jego własny.
                foreach (var range in SecondStagePlanner.DefaultRanges(secondStage, firstStage.Select(g => teams(g.Id)).DefaultIfEmpty(0).Max()))
                {
                    var created = SecondStageRange.Create(Guid.NewGuid(), tournamentId, category.Key, range.GroupId, range.From, range.To);
                    if (created.IsSuccess)
                        ranges.Add(created.Value);
                }
                await dbContext.AddSecondStageRangesAsync(ranges, cancellationToken);
            }

            dbContext.RemoveSecondStageSeats(seats);
            await dbContext.SaveChangesAsync(cancellationToken);

            var planned = SecondStagePlanner.SeatsFromRanges(
                ranges.Select(r => new SecondStagePlanner.Range(r.GroupId, r.FromPosition, r.ToPosition)).ToList(),
                secondStage, firstStage, teams);
            var newSeats = new List<SecondStageSeat>();
            foreach (var (seat, order) in planned.Select((s, i) => (s, i + 1)))
            {
                var created = SecondStageSeat.Create(
                    Guid.NewGuid(), tournamentId, category.Key, seat.GroupId, order, seat.SourceGroupId, seat.SourcePosition);
                if (created.IsSuccess)
                    newSeats.Add(created.Value);
            }
            await dbContext.AddSecondStageSeatsAsync(newSeats, cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task<IReadOnlyList<SecondStageFixtureInfoDto>> GetSecondStageFixturesAsync(Guid tournamentId, CancellationToken cancellationToken = default) =>
        (await dbContext.GetSecondStageFixturesAsync(tournamentId, cancellationToken))
            .Select(f => new SecondStageFixtureInfoDto(
                f.Id, f.CategoryId, f.GroupId, f.HomeSeatId, f.AwaySeatId, f.Round,
                f.PlannedPitchId, f.PlannedAt, f.PlannedRefereeId, f.MatchId))
            .ToList();

    public async Task CreateSecondStageFixturesAsync(
        Guid tournamentId, IReadOnlyCollection<SecondStageFixturePlanDto> fixtures, CancellationToken cancellationToken = default)
    {
        dbContext.RemoveSecondStageFixtures(await dbContext.GetTrackedSecondStageFixturesAsync(tournamentId, cancellationToken));

        var created = new List<SecondStageFixture>();
        foreach (var plan in fixtures)
        {
            var fixture = SecondStageFixture.Create(
                plan.Id, tournamentId, plan.CategoryId, plan.GroupId, plan.HomeSeatId, plan.AwaySeatId, plan.Round, plan.PitchId, plan.ScheduledAt);
            if (fixture.IsSuccess)
                created.Add(fixture.Value);
        }

        await dbContext.AddSecondStageFixturesAsync(created, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    // Mecze fazy 2 znikają razem z terminarzem, więc obsada miejsc (z tabel fazy 1) też przestaje obowiązywać.
    private async Task ClearSecondStageAsync(Guid tournamentId, CancellationToken cancellationToken)
    {
        var fixtures = await dbContext.GetTrackedSecondStageFixturesAsync(tournamentId, cancellationToken);
        var seats = (await dbContext.GetTrackedSecondStageSeatsAsync(tournamentId, cancellationToken))
            .Where(s => s.TournamentTeamId.HasValue)
            .ToList();
        if (fixtures.Count == 0 && seats.Count == 0)
            return;

        dbContext.RemoveSecondStageFixtures(fixtures);
        foreach (var seat in seats)
            seat.Resolve(null);
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

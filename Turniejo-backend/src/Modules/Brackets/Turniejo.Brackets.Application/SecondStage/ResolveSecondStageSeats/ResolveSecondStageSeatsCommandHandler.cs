using System.Text.Json;
using Turniejo.Brackets.Application.Abstractions;
using Turniejo.Brackets.Domain.Events;
using Turniejo.SharedKernel.Contracts;
using Turniejo.SharedKernel.CQRS;
using Turniejo.SharedKernel.Outbox;
using Turniejo.SharedKernel.Results;

namespace Turniejo.Brackets.Application.SecondStage.ResolveSecondStageSeats
{
    /// <summary>
    /// Po zmianie tabeli grupy fazy 1: gdy grupa rozegrała wszystkie mecze, jej drużyny trafiają na swoje
    /// miejsca w fazie 2, a zaplanowany mecz fazy 2 powstaje, gdy oba miejsca mają drużyny. Korekta wyniku
    /// po fazie 1 przestawia drużyny: nierozpoczęty mecz dostaje nową, rozpoczęty zostaje, a organizator
    /// dostaje powiadomienie (jak w pucharze, GPK-008).
    /// </summary>
    internal sealed class ResolveSecondStageSeatsCommandHandler(
        IBracketsDbContext dbContext,
        IMatchModule matchModule,
        IStandingModule standingModule) : ICommandHandler<ResolveSecondStageSeatsCommand, bool>
    {
        public async Task<Result<bool>> Handle(ResolveSecondStageSeatsCommand request, CancellationToken cancellationToken)
        {
            var seats = await dbContext.GetTrackedSecondStageSeatsAsync(request.TournamentId, cancellationToken);
            var fromGroup = seats.Where(s => s.SourceGroupId == request.GroupId).ToList();
            if (fromGroup.Count == 0)
                return Result.Success(false);

            // Miejsce w tabeli w trakcie fazy grupowej jeszcze nic nie znaczy.
            if (!await matchModule.IsGroupStageFinishedAsync(request.GroupId, cancellationToken))
                return Result.Success(false);

            var standings = await standingModule.GetGroupStandingsAsync(request.GroupId, cancellationToken);
            var changedSeatIds = new HashSet<Guid>();
            foreach (var seat in fromGroup)
            {
                var expected = standings.FirstOrDefault(s => s.Position == seat.SourcePosition)?.TournamentTeamId;
                if (expected is null || expected == seat.TournamentTeamId)
                    continue;

                seat.Resolve(expected);
                changedSeatIds.Add(seat.Id);
            }

            if (changedSeatIds.Count == 0)
                return Result.Success(false);

            var seatById = seats.ToDictionary(s => s.Id);
            var fixtures = await dbContext.GetTrackedSecondStageFixturesByGroupIdsAsync(
                fromGroup.Select(s => s.GroupId).Distinct().ToList(), cancellationToken);
            var blockedMatchIds = new List<Guid>();
            foreach (var fixture in fixtures.Where(f => changedSeatIds.Contains(f.HomeSeatId) || changedSeatIds.Contains(f.AwaySeatId)))
            {
                if (seatById[fixture.HomeSeatId].TournamentTeamId is not { } home ||
                    seatById[fixture.AwaySeatId].TournamentTeamId is not { } away)
                    continue;

                if (fixture.MatchId is { } matchId)
                {
                    if (!await matchModule.ChangeUnstartedMatchTeamsAsync(matchId, home, away, cancellationToken))
                        blockedMatchIds.Add(matchId);
                    continue;
                }

                var createdId = await matchModule.CreateSecondStageMatchAsync(
                    request.TournamentId, fixture.CategoryId, fixture.GroupId, home, away,
                    fixture.PlannedPitchId, fixture.PlannedAt, fixture.PlannedRefereeId, cancellationToken);
                fixture.AssignMatch(createdId);
            }

            if (blockedMatchIds.Count > 0)
                await AddEventAsync(new BracketSlotChangeBlocked(request.TournamentId, blockedMatchIds, SecondStage: true), cancellationToken);

            // Widoki odświeżają się tym samym sygnałem co drabinka po awansie z grup.
            await AddEventAsync(new TeamsAdvancedToBracket(Guid.Empty, request.TournamentId), cancellationToken);

            await dbContext.SaveChangesAsync(cancellationToken);
            return Result.Success(true);
        }

        private Task AddEventAsync<T>(T domainEvent, CancellationToken cancellationToken) where T : notnull =>
            dbContext.AddOutboxMessageAsync(
                OutboxMessage.Create(domainEvent.GetType().AssemblyQualifiedName!, JsonSerializer.Serialize(domainEvent, domainEvent.GetType())),
                cancellationToken);
    }
}

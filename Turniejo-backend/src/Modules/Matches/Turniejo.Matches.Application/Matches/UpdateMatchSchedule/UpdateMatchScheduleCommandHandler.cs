using System.Text.Json;
using Turniejo.Matches.Application.Abstractions;
using Turniejo.Matches.Application.Services;
using Turniejo.Matches.Domain.Entities;
using Turniejo.Matches.Domain.Enums;
using Turniejo.SharedKernel.Contracts;
using Turniejo.SharedKernel.CQRS;
using Turniejo.SharedKernel.Outbox;
using Turniejo.SharedKernel.Results;

namespace Turniejo.Matches.Application.Matches.UpdateMatchSchedule
{
    internal sealed class UpdateMatchScheduleCommandHandler(
        IMatchesDbContext dbContext,
        ScheduleConflictChecker conflictChecker,
        PlannedSlotShifter plannedSlotShifter,
        PitchRefereeAssigner pitchRefereeAssigner,
        ICurrentUserContext currentUserContext) : ICommandHandler<UpdateMatchScheduleCommand, UpdateMatchScheduleResponse>
    {
        public async Task<Result<UpdateMatchScheduleResponse>> Handle(UpdateMatchScheduleCommand request, CancellationToken cancellationToken)
        {
            var match = await dbContext.GetMatchByIdAsync(request.MatchId, cancellationToken);
            if (match == null || match.TournamentId != request.TournamentId)
                return Result.Failure<UpdateMatchScheduleResponse>(Error.NotFound("Match", request.MatchId));

            var delta = request.ScheduledAt - match.ScheduledAt;
            var moves = new List<ScheduleMove> { new(match, request.PitchId, request.ScheduledAt) };
            var otherPitches = false;
            if (request.ShiftFollowing && match.PitchId.HasValue && request.PitchId == match.PitchId && delta != TimeSpan.Zero)
            {
                (var shifted, otherPitches) = await PlanShiftAsync(match, delta, cancellationToken);
                moves.AddRange(shifted.Select(f => new ScheduleMove(f, f.PitchId, f.ScheduledAt + delta)));
            }
            var followers = moves.Skip(1).ToList();

            var conflict = await conflictChecker.FindConflictAsync(moves, "PitchId", cancellationToken);
            if (conflict != null)
                return Result.Failure<UpdateMatchScheduleResponse>(conflict);

            // Pytamy dopiero, gdy wiadomo, że przesunięcie się uda - inaczej po potwierdzeniu przyszedłby błąd.
            if (otherPitches && !request.ShiftOtherPitches)
                return Result.Failure<UpdateMatchScheduleResponse>(
                    Error.Conflict("OtherPitches", "The shift also moves matches on other pitches"));

            if (followers.Count == 0)
            {
                var previousPitchId = match.PitchId;
                var result = await ApplyAsync(moves[0], cancellationToken);
                if (result.IsFailure)
                    return Result.Failure<UpdateMatchScheduleResponse>(result.Error);

                // Mecz przeniesiony na inne boisko dostaje jego sędziego (o ile nie ma ręcznie wybranego innego).
                if (previousPitchId != match.PitchId)
                {
                    var pitches = await dbContext.GetPitchesByTournamentIdAsync(match.TournamentId, cancellationToken);
                    Guid? RefereeOf(Guid? pitchId) => pitches.FirstOrDefault(p => p.Id == pitchId)?.RefereeId;
                    await pitchRefereeAssigner.ApplyAsync(
                        [new PitchRefereeTarget(match, RefereeOf(match.PitchId), RefereeOf(previousPitchId))],
                        currentUserContext.UserId, cancellationToken);
                }
                await dbContext.SaveChangesAsync(cancellationToken);
            }
            else
            {
                var failure = await SaveShiftAsync(moves, delta, cancellationToken);
                if (failure != null)
                    return Result.Failure<UpdateMatchScheduleResponse>(failure);
            }

            await plannedSlotShifter.ShiftAsync(match.TournamentId, cancellationToken);

            return Result.Success(new UpdateMatchScheduleResponse(match.Id));
        }

        // Późniejsze mecze na tym boisku, które da się jeszcze przesunąć. Trwający albo zakończony mecz
        // zostaje na miejscu - jeśli przesunięte mecze na niego najadą, zmianę odrzuci sprawdzanie kolizji.
        //
        // Przy opóźnieniu przesunięty mecz może najechać na mecz tej samej drużyny albo sędziego na innym
        // boisku (drużyna gra na A, zaraz potem na B). Wtedy przesuwamy też ten mecz i późniejsze na jego
        // boisku, o tyle samo, aż nic nie koliduje. Inne boiska ruszamy tylko od meczu, który faktycznie
        // koliduje - wcześniejsze mecze i boiska bez kolizji zostają. Przesunięte o tyle samo mecze nie
        // kolidują między sobą (wcześniej też nie kolidowały), więc pętla dodaje tylko nowe mecze i się kończy.
        private async Task<(List<Match> Shifted, bool OtherPitches)> PlanShiftAsync(Match match, TimeSpan delta, CancellationToken cancellationToken)
        {
            var all = await dbContext.GetMatchesByTournamentIdAsync(match.TournamentId, cancellationToken);
            var planned = new Dictionary<Guid, Match> { [match.Id] = match };
            var otherPitches = false;

            void AddFrom(Match start)
            {
                planned.TryAdd(start.Id, start);
                if (!start.PitchId.HasValue)
                    return;
                foreach (var m in all.Where(m => m.PitchId == start.PitchId && m.ScheduledAt > start.ScheduledAt && IsSchedulable(m)))
                    planned.TryAdd(m.Id, m);
            }

            AddFrom(match);

            // Na wcześniej nic nie przesuwamy na innych boiskach - kolizja zostaje błędem.
            while (delta > TimeSpan.Zero)
            {
                var moves = planned.Values.Select(m => new ScheduleMove(m, m.PitchId, m.ScheduledAt + delta)).ToList();
                var blockers = await conflictChecker.FindBlockingMatchesAsync(moves, cancellationToken);
                var next = blockers.Where(IsSchedulable).ToList();
                if (next.Count == 0 || next.Count < blockers.Count)
                    break;

                foreach (var blocker in next)
                {
                    otherPitches |= blocker.PitchId != match.PitchId;
                    AddFrom(blocker);
                }
            }

            planned.Remove(match.Id);
            var ids = planned.Keys.ToList();
            var tracked = ids.Count == 0 ? [] : (await dbContext.GetTrackedMatchesByIdsAsync(ids, cancellationToken)).ToList();
            return (tracked, otherPitches);
        }

        private static bool IsSchedulable(Match m) =>
            m.Status is MatchStatus.Scheduled or MatchStatus.Ready or MatchStatus.Postponed;

        // Unikalny indeks (PitchId, ScheduledAt) sprawdza każdy UPDATE osobno. Przy +30 min mecz z 10:30
        // trafiłby na 11:00, gdzie jeszcze stoi następny, więc zapisujemy po kolei od końca (przy
        // przesunięciu na wcześniej od początku), w jednej transakcji.
        private async Task<Error?> SaveShiftAsync(List<ScheduleMove> moves, TimeSpan delta, CancellationToken cancellationToken)
        {
            var ordered = delta > TimeSpan.Zero
                ? moves.OrderByDescending(m => m.Match.ScheduledAt).ToList()
                : moves.OrderBy(m => m.Match.ScheduledAt).ToList();

            Error? failure = null;
            await dbContext.ExecuteInTransactionAsync(async () =>
            {
                foreach (var move in ordered)
                {
                    var result = await ApplyAsync(move, cancellationToken);
                    if (result.IsFailure)
                    {
                        failure = result.Error;
                        return;
                    }
                    await dbContext.SaveChangesAsync(cancellationToken);
                }
            }, cancellationToken);

            return failure;
        }

        private async Task<Result> ApplyAsync(ScheduleMove move, CancellationToken cancellationToken)
        {
            var result = move.Match.UpdateSchedule(move.PitchId, move.ScheduledAt, currentUserContext.UserId);
            if (result.IsFailure)
                return result;

            foreach (var domainEvent in move.Match.DomainEvents)
            {
                var outboxMessage = OutboxMessage.Create(
                    domainEvent.GetType().AssemblyQualifiedName!,
                    JsonSerializer.Serialize(domainEvent, domainEvent.GetType()));

                await dbContext.AddOutboxMessageAsync(outboxMessage, cancellationToken);
            }
            move.Match.ClearDomainEvents();

            return Result.Success();
        }
    }
}

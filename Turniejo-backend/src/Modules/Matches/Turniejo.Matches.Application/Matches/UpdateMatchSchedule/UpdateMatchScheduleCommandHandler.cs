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
        ICurrentUserContext currentUserContext) : ICommandHandler<UpdateMatchScheduleCommand, UpdateMatchScheduleResponse>
    {
        public async Task<Result<UpdateMatchScheduleResponse>> Handle(UpdateMatchScheduleCommand request, CancellationToken cancellationToken)
        {
            var match = await dbContext.GetMatchByIdAsync(request.MatchId, cancellationToken);
            if (match == null || match.TournamentId != request.TournamentId)
                return Result.Failure<UpdateMatchScheduleResponse>(Error.NotFound("Match", request.MatchId));

            var delta = request.ScheduledAt - match.ScheduledAt;
            var followers = request.ShiftFollowing && match.PitchId.HasValue && request.PitchId == match.PitchId && delta != TimeSpan.Zero
                ? await GetFollowersAsync(match, cancellationToken)
                : [];

            var moves = followers
                .Select(f => new ScheduleMove(f, f.PitchId, f.ScheduledAt + delta))
                .Prepend(new ScheduleMove(match, request.PitchId, request.ScheduledAt))
                .ToList();

            var conflict = await conflictChecker.FindConflictAsync(moves, "PitchId", cancellationToken);
            if (conflict != null)
                return Result.Failure<UpdateMatchScheduleResponse>(conflict);

            if (followers.Count == 0)
            {
                var result = await ApplyAsync(moves[0], cancellationToken);
                if (result.IsFailure)
                    return Result.Failure<UpdateMatchScheduleResponse>(result.Error);
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
        private async Task<List<Match>> GetFollowersAsync(Match match, CancellationToken cancellationToken)
        {
            var ids = (await dbContext.GetMatchesByTournamentIdAsync(match.TournamentId, cancellationToken))
                .Where(m => m.Id != match.Id && m.PitchId == match.PitchId && m.ScheduledAt > match.ScheduledAt)
                .Where(m => m.Status is MatchStatus.Scheduled or MatchStatus.Ready or MatchStatus.Postponed)
                .Select(m => m.Id)
                .ToList();

            return ids.Count == 0
                ? []
                : (await dbContext.GetTrackedMatchesByIdsAsync(ids, cancellationToken)).ToList();
        }

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

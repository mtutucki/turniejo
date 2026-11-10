using Turniejo.Brackets.Application.Abstractions;
using Turniejo.Brackets.Domain.Entities;
using Turniejo.SharedKernel.Contracts;
using Turniejo.SharedKernel.CQRS;
using Turniejo.SharedKernel.Results;

namespace Turniejo.Brackets.Application.SecondStage.SetSecondStageSeats
{
    internal sealed class SetSecondStageSeatsCommandHandler(
        IBracketsDbContext dbContext,
        ITournamentModule tournamentModule) : ICommandHandler<SetSecondStageSeatsCommand, int>
    {
        private static readonly HashSet<string> ScheduleStatuses = ["ScheduleGenerated", "InProgress", "Finished"];

        public async Task<Result<int>> Handle(SetSecondStageSeatsCommand request, CancellationToken cancellationToken)
        {
            var tournament = await tournamentModule.GetTournamentAsync(request.TournamentId, cancellationToken);
            if (tournament == null)
                return Result.Failure<int>(Error.NotFound("Tournament", request.TournamentId));

            if (tournament.Format != "TwoGroupStages")
                return Result.Failure<int>(Error.Validation("Format", "Second-stage groups need the two group stages format"));

            // Mecze fazy 2 powstały z tego podziału (jak grupy po wygenerowaniu terminarza).
            if (ScheduleStatuses.Contains(tournament.Status))
                return Result.Failure<int>(Error.Conflict("Schedule", "Clear the schedule before changing the second stage"));

            var groups = (await tournamentModule.GetTournamentGroupsAsync(request.TournamentId, cancellationToken))
                .Where(g => g.CategoryId == request.CategoryId)
                .ToDictionary(g => g.Id);

            if (request.Seats.Any(s => !groups.TryGetValue(s.GroupId, out var g) || g.Stage != 2))
                return Result.Failure<int>(Error.Validation("GroupId", "Each place must go to a second-stage group of this category"));

            if (request.Seats.Any(s => !groups.TryGetValue(s.SourceGroupId, out var g) || g.Stage != 1))
                return Result.Failure<int>(Error.Validation("SourceGroupId", "Each place must come from a first-stage group of this category"));

            if (request.Seats.GroupBy(s => (s.SourceGroupId, s.SourcePosition)).Any(g => g.Count() > 1))
                return Result.Failure<int>(Error.Validation("Seats", "Each place can go to only one second-stage group"));

            var existing = (await dbContext.GetTrackedSecondStageSeatsAsync(request.TournamentId, cancellationToken))
                .Where(s => s.CategoryId == request.CategoryId)
                .ToList();
            dbContext.RemoveSecondStageSeats(existing);
            // Jeden tryb na kategorię: pojedynczo ustawione miejsca zastępują zakresy.
            dbContext.RemoveSecondStageRanges((await dbContext.GetTrackedSecondStageRangesAsync(request.TournamentId, cancellationToken))
                .Where(r => r.CategoryId == request.CategoryId));
            // Usunięcia idą do bazy przed nowymi wierszami: to samo miejsce z fazy 1 (unikalny indeks)
            // zwykle wraca w nowym podziale, tylko w innej grupie albo kolejności.
            await dbContext.SaveChangesAsync(cancellationToken);

            var seats = new List<SecondStageSeat>();
            foreach (var (input, order) in request.Seats.Select((s, i) => (s, i + 1)))
            {
                var seat = SecondStageSeat.Create(
                    Guid.NewGuid(), request.TournamentId, request.CategoryId, input.GroupId, order, input.SourceGroupId, input.SourcePosition);
                if (seat.IsFailure)
                    return Result.Failure<int>(seat.Error);
                seats.Add(seat.Value);
            }

            await dbContext.AddSecondStageSeatsAsync(seats, cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);
            return Result.Success(seats.Count);
        }
    }
}

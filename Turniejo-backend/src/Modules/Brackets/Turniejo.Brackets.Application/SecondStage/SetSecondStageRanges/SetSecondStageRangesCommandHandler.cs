using Turniejo.Brackets.Application.Abstractions;
using Turniejo.Brackets.Domain.Entities;
using Turniejo.SharedKernel.Contracts;
using Turniejo.SharedKernel.CQRS;
using Turniejo.SharedKernel.Results;

namespace Turniejo.Brackets.Application.SecondStage.SetSecondStageRanges
{
    internal sealed class SetSecondStageRangesCommandHandler(
        IBracketsDbContext dbContext,
        ITournamentModule tournamentModule) : ICommandHandler<SetSecondStageRangesCommand, int>
    {
        private static readonly HashSet<string> ScheduleStatuses = ["ScheduleGenerated", "InProgress", "Finished"];

        public async Task<Result<int>> Handle(SetSecondStageRangesCommand request, CancellationToken cancellationToken)
        {
            var tournament = await tournamentModule.GetTournamentAsync(request.TournamentId, cancellationToken);
            if (tournament == null)
                return Result.Failure<int>(Error.NotFound("Tournament", request.TournamentId));

            if (tournament.Format != "TwoGroupStages")
                return Result.Failure<int>(Error.Validation("Format", "Second-stage groups need the two group stages format"));

            if (ScheduleStatuses.Contains(tournament.Status))
                return Result.Failure<int>(Error.Conflict("Schedule", "Clear the schedule before changing the second stage"));

            var groups = (await tournamentModule.GetTournamentGroupsAsync(request.TournamentId, cancellationToken))
                .Where(g => g.CategoryId == request.CategoryId)
                .ToDictionary(g => g.Id);
            if (request.Ranges.Any(r => !groups.TryGetValue(r.GroupId, out var g) || g.Stage != 2))
                return Result.Failure<int>(Error.Validation("GroupId", "Each place must go to a second-stage group of this category"));

            if (request.Ranges.GroupBy(r => r.GroupId).Any(g => g.Count() > 1))
                return Result.Failure<int>(Error.Validation("Ranges", "Each second-stage group can have only one range of places"));

            if (request.Ranges.Any(r => r.ToPosition < r.FromPosition))
                return Result.Failure<int>(Error.Validation("Ranges", "A range of places must go from a lower place to a higher one"));

            if (SecondStagePlanner.Overlap(request.Ranges.Select(r => new SecondStagePlanner.Range(r.GroupId, r.FromPosition, r.ToPosition)).ToList()))
                return Result.Failure<int>(Error.Validation("Ranges", "Each place can go to only one second-stage group"));

            // Jeden tryb na kategorię: zakresy zastępują pojedynczo ustawione miejsca.
            var oldRanges = (await dbContext.GetTrackedSecondStageRangesAsync(request.TournamentId, cancellationToken))
                .Where(r => r.CategoryId == request.CategoryId);
            var oldSeats = (await dbContext.GetTrackedSecondStageSeatsAsync(request.TournamentId, cancellationToken))
                .Where(s => s.CategoryId == request.CategoryId);
            dbContext.RemoveSecondStageRanges(oldRanges);
            dbContext.RemoveSecondStageSeats(oldSeats);
            await dbContext.SaveChangesAsync(cancellationToken);

            var ranges = new List<SecondStageRange>();
            foreach (var input in request.Ranges)
            {
                var range = SecondStageRange.Create(
                    Guid.NewGuid(), request.TournamentId, request.CategoryId, input.GroupId, input.FromPosition, input.ToPosition);
                if (range.IsFailure)
                    return Result.Failure<int>(range.Error);
                ranges.Add(range.Value);
            }

            await dbContext.AddSecondStageRangesAsync(ranges, cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);
            return Result.Success(ranges.Count);
        }
    }
}

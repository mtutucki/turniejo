using Turniejo.Matches.Application.Abstractions;
using Turniejo.Matches.Application.Services;
using Turniejo.SharedKernel.Contracts;
using Turniejo.SharedKernel.CQRS;
using Turniejo.SharedKernel.Results;

namespace Turniejo.Matches.Application.Matches.UpdatePlannedMatchSchedule
{
    internal sealed class UpdatePlannedMatchScheduleCommandHandler(
        IMatchesDbContext dbContext,
        PlannedMatchContext plannedMatchContext,
        PlannedSlotShifter plannedSlotShifter,
        IBracketModule bracketModule) : ICommandHandler<UpdatePlannedMatchScheduleCommand, UpdatePlannedMatchScheduleResponse>
    {
        public async Task<Result<UpdatePlannedMatchScheduleResponse>> Handle(UpdatePlannedMatchScheduleCommand request, CancellationToken cancellationToken)
        {
            var loaded = await plannedMatchContext.LoadAsync(request.TournamentId, request.NodeId, cancellationToken);
            if (loaded.IsFailure)
                return Result.Failure<UpdatePlannedMatchScheduleResponse>(loaded.Error);
            var ctx = loaded.Value;
            var start = request.ScheduledAt;

            var pitch = await dbContext.GetPitchByIdAsync(request.PitchId, cancellationToken);
            if (pitch == null || pitch.TournamentId != request.TournamentId)
                return Result.Failure<UpdatePlannedMatchScheduleResponse>(Error.Validation("PitchId", "Pitch does not belong to this tournament"));
            if (!pitch.IsActive && pitch.Id != ctx.Node.PlannedPitchId)
                return Result.Failure<UpdatePlannedMatchScheduleResponse>(Error.Validation("PitchId", "Pitch is inactive"));

            if (ctx.Tournament.StartAt is { } tournamentStart && ctx.Tournament.EndAt is { } tournamentEnd &&
                (start < tournamentStart || start + ctx.Duration > tournamentEnd))
                return Result.Failure<UpdatePlannedMatchScheduleResponse>(Error.Validation("ScheduledAt", "Match must take place within the tournament dates"));

            // Drużyny przyjdą z meczów poprzedniej rundy, więc nie wcześniej niż po nich (z przerwą, jak
            // w generatorze; inaczej PlannedSlotShifter i tak odsunąłby termin).
            var matchStart = ctx.Matches.ToDictionary(m => m.Id, m => m.ScheduledAt);
            DateTimeOffset? SourceReadyAt(Guid? sourceId)
            {
                var source = ctx.Nodes.FirstOrDefault(n => n.Id == sourceId);
                if (source?.MatchId is { } matchId && matchStart.TryGetValue(matchId, out var at))
                    return at + ctx.Slot;
                return source?.PlannedAt + ctx.Slot;
            }
            var readyAt = new[] { SourceReadyAt(ctx.Node.HomeSourceNodeId), SourceReadyAt(ctx.Node.AwaySourceNodeId) }
                .Where(at => at.HasValue)
                .Select(at => at!.Value)
                .DefaultIfEmpty(DateTimeOffset.MinValue)
                .Max();
            if (start < readyAt)
                return Result.Failure<UpdatePlannedMatchScheduleResponse>(Error.Validation("ScheduledAt", "Match must start after the matches its teams come from"));

            // Ten sam warunek co w PlannedSlotShifter: po meczu na boisku jest jeszcze przerwa.
            bool Blocks(DateTimeOffset other) => start < other + ctx.Slot && other < start + ctx.Duration;
            var pitchTaken =
                ctx.Matches.Any(m => m.PitchId == pitch.Id && Blocks(m.ScheduledAt)) ||
                ctx.Nodes.Any(n => n.Id != ctx.Node.Id && !n.MatchId.HasValue && n.PlannedPitchId == pitch.Id &&
                                   n.PlannedAt is { } at && Blocks(at));
            if (pitchTaken)
                return Result.Failure<UpdatePlannedMatchScheduleResponse>(Error.Validation("PitchId", "Another match is already scheduled on this pitch at this time"));

            if (ctx.Node.PlannedRefereeId is { } refereeId && PlannedMatchContext.RefereeBusy(ctx, refereeId, start))
                return Result.Failure<UpdatePlannedMatchScheduleResponse>(Error.Validation("ScheduledAt", "The referee already has another match at this time"));

            await bracketModule.SetPlannedSlotsAsync([new BracketPlannedSlotDto(ctx.Node.Id, pitch.Id, start)], cancellationToken);
            // Kolejne rundy (np. finał po tym półfinale) odsuwają się, jeśli teraz wypadałyby za wcześnie.
            await plannedSlotShifter.ShiftAsync(request.TournamentId, cancellationToken);

            return Result.Success(new UpdatePlannedMatchScheduleResponse(ctx.Node.Id));
        }
    }
}

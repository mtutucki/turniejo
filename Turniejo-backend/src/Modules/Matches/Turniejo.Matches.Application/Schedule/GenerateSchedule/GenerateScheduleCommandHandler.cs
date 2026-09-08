using System.Text.Json;
using Turniejo.Matches.Application.Abstractions;
using Turniejo.Matches.Domain.Entities;
using Turniejo.Matches.Domain.Enums;
using Turniejo.Matches.Domain.Events;
using Turniejo.SharedKernel.Contracts;
using Turniejo.SharedKernel.CQRS;
using Turniejo.SharedKernel.Outbox;
using Turniejo.SharedKernel.Results;

namespace Turniejo.Matches.Application.Schedule.GenerateSchedule
{
    internal sealed class GenerateScheduleCommandHandler(
        IMatchesDbContext dbContext,
        ITournamentModule tournamentModule,
        ITeamModule teamModule,
        ICurrentUserContext currentUserContext) : ICommandHandler<GenerateScheduleCommand, GenerateScheduleResponse>
    {
        private sealed record MatchPair(Guid CategoryId, Guid GroupId, Guid HomeTournamentTeamId, Guid AwayTournamentTeamId);

        public async Task<Result<GenerateScheduleResponse>> Handle(GenerateScheduleCommand request, CancellationToken cancellationToken)
        {
            var tournament = await tournamentModule.GetTournamentAsync(request.TournamentId, cancellationToken);
            if (tournament == null)
                return Result.Failure<GenerateScheduleResponse>(Error.NotFound("Tournament", request.TournamentId));

            if (tournament.Format != "Group" && tournament.Format != "GroupAndKnockout")
                return Result.Failure<GenerateScheduleResponse>(Error.Validation("Format", "Round-robin schedule generation requires tournament format Group or GroupAndKnockout"));

            var settings = await tournamentModule.GetTournamentSettingsAsync(request.TournamentId, cancellationToken);
            if (settings == null)
                return Result.Failure<GenerateScheduleResponse>(Error.NotFound("TournamentSettings", request.TournamentId));

            var allPitches = await dbContext.GetPitchesByIdsAsync(request.PitchIds, cancellationToken);
            var pitches = allPitches.Where(p => p.TournamentId == request.TournamentId && p.IsActive).ToList();
            if (pitches.Count == 0)
                return Result.Failure<GenerateScheduleResponse>(Error.Validation("PitchIds", "No valid active pitches belonging to this tournament were provided"));

            var groups = await tournamentModule.GetTournamentGroupsAsync(request.TournamentId, cancellationToken);
            if (groups.Count == 0)
                return Result.Failure<GenerateScheduleResponse>(Error.Validation("TournamentId", "Tournament has no groups defined"));

            var allTournamentTeams = await teamModule.GetTournamentTeamsByTournamentIdAsync(request.TournamentId, cancellationToken);
            var acceptedTeamsByGroup = allTournamentTeams
                .Where(t => t.RegistrationStatus == "Accepted" && t.GroupId.HasValue)
                .GroupBy(t => t.GroupId!.Value)
                .ToDictionary(g => g.Key, g => g.Select(t => t.Id).ToList());

            var pairs = new List<MatchPair>();
            foreach (var group in groups)
            {
                if (!acceptedTeamsByGroup.TryGetValue(group.Id, out var teamIds) || teamIds.Count < 2)
                    continue;

                for (var i = 0; i < teamIds.Count; i++)
                {
                    for (var j = i + 1; j < teamIds.Count; j++)
                    {
                        pairs.Add(new MatchPair(group.CategoryId, group.Id, teamIds[i], teamIds[j]));
                    }
                }
            }

            if (pairs.Count == 0)
                return Result.Failure<GenerateScheduleResponse>(Error.Validation("TournamentId", "No matches to schedule — each group needs at least 2 accepted teams"));

            var slotMinutes = settings.MatchDurationMinutes + settings.BreakDurationMinutes;
            var existingMatches = await dbContext.GetMatchesByTournamentIdAsync(request.TournamentId, cancellationToken);
            var occupiedSlots = existingMatches
                .Where(m => m.PitchId.HasValue)
                .Select(m => (m.PitchId!.Value, m.ScheduledAt))
                .ToHashSet();

            var scheduledMatches = new List<Match>();
            var pairIndex = 0;

            for (var slotStart = request.StartAt; slotStart < request.EndAt && pairIndex < pairs.Count; slotStart = slotStart.AddMinutes(slotMinutes))
            {
                foreach (var pitch in pitches)
                {
                    if (pairIndex >= pairs.Count)
                        break;

                    if (occupiedSlots.Contains((pitch.Id, slotStart)))
                        continue;

                    var pair = pairs[pairIndex];
                    var matchResult = Match.Create(
                        Guid.NewGuid(), request.TournamentId, pair.CategoryId, pair.GroupId,
                        pair.HomeTournamentTeamId, pair.AwayTournamentTeamId, pitch.Id,
                        slotStart, StageType.Group);

                    if (matchResult.IsFailure)
                        return Result.Failure<GenerateScheduleResponse>(matchResult.Error);

                    scheduledMatches.Add(matchResult.Value);
                    occupiedSlots.Add((pitch.Id, slotStart));
                    pairIndex++;
                }
            }

            if (pairIndex < pairs.Count)
                return Result.Failure<GenerateScheduleResponse>(Error.Validation(
                    "EndAt", $"Not enough time available to schedule all matches — {pairs.Count - pairIndex} match(es) could not be placed"));

            var statusChanged = await tournamentModule.ChangeTournamentStatusAsync(request.TournamentId, "ScheduleGenerated", cancellationToken);
            if (!statusChanged)
                return Result.Failure<GenerateScheduleResponse>(Error.Conflict("Status", "Tournament cannot transition to ScheduleGenerated from its current status"));

            await dbContext.AddMatchesAsync(scheduledMatches, cancellationToken);

            var scheduleGenerated = new ScheduleGenerated(request.TournamentId, scheduledMatches.Count, currentUserContext.UserId);
            var outboxMessage = OutboxMessage.Create(
                scheduleGenerated.GetType().AssemblyQualifiedName!,
                JsonSerializer.Serialize(scheduleGenerated, scheduleGenerated.GetType()));
            await dbContext.AddOutboxMessageAsync(outboxMessage, cancellationToken);

            await dbContext.SaveChangesAsync(cancellationToken);

            return Result.Success(new GenerateScheduleResponse(scheduledMatches.Count));
        }
    }
}

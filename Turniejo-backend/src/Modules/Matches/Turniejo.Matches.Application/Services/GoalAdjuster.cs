using System.Text.Json;
using Turniejo.Matches.Application.Abstractions;
using Turniejo.Matches.Application.Matches;
using Turniejo.Matches.Domain.Entities;
using Turniejo.Matches.Domain.Enums;
using Turniejo.SharedKernel.Outbox;

namespace Turniejo.Matches.Application.Services
{
    /// <summary>
    /// Doprowadza gole meczu do podanego wyniku: brakujące dopisuje bez strzelca, nadmiarowe cofa -
    /// najpierw te bez strzelca, potem najpóźniejsze, żeby nie gubić uzupełnionych statystyk.
    /// Wynik meczu liczy się potem od nowa ze zdarzeń (RecalculateMatchScoreCommand).
    /// </summary>
    public sealed class GoalAdjuster(IMatchesDbContext dbContext)
    {
        public async Task AdjustToAsync(
            Match match, IReadOnlyList<MatchEvent> events, int homeGoals, int awayGoals,
            int minute, Guid userId, CancellationToken cancellationToken)
        {
            var (currentHome, currentAway) = MatchGoalScore.FromEvents(match, events);
            await AdjustTeamAsync(match, events, match.HomeTournamentTeamId, homeGoals - currentHome, minute, userId, cancellationToken);
            await AdjustTeamAsync(match, events, match.AwayTournamentTeamId, awayGoals - currentAway, minute, userId, cancellationToken);
        }

        private async Task AdjustTeamAsync(
            Match match, IReadOnlyList<MatchEvent> events, Guid teamId, int difference, int minute, Guid userId,
            CancellationToken cancellationToken)
        {
            for (var i = 0; i < difference; i++)
            {
                var goal = MatchEvent.Create(Guid.NewGuid(), match.Id, MatchEventType.Goal, minute, userId, teamId).Value;
                await dbContext.AddMatchEventAsync(goal, cancellationToken);
            }

            if (difference >= 0)
                return;

            var toRevert = events
                .Where(e => ScoresFor(match, e) == teamId)
                .OrderBy(e => e.PlayerId.HasValue)
                .ThenByDescending(e => e.Minute)
                .ThenByDescending(e => e.Second ?? 0)
                .Take(-difference)
                .Select(e => e.Id)
                .ToList();

            foreach (var eventId in toRevert)
            {
                // Lista zdarzeń jest bez śledzenia zmian, więc usuwane zdarzenie pobieramy jeszcze raz.
                var tracked = await dbContext.GetMatchEventByIdAsync(eventId, cancellationToken);
                if (tracked == null || tracked.MarkDeleted(userId, match.TournamentId).IsFailure)
                    continue;

                foreach (var domainEvent in tracked.DomainEvents)
                {
                    await dbContext.AddOutboxMessageAsync(
                        OutboxMessage.Create(domainEvent.GetType().AssemblyQualifiedName!, JsonSerializer.Serialize(domainEvent, domainEvent.GetType())),
                        cancellationToken);
                }
            }
        }

        private static Guid? ScoresFor(Match match, MatchEvent matchEvent) => matchEvent.Type switch
        {
            MatchEventType.Goal or MatchEventType.PenaltyGoal => matchEvent.TournamentTeamId,
            MatchEventType.OwnGoal when matchEvent.TournamentTeamId == match.HomeTournamentTeamId => match.AwayTournamentTeamId,
            MatchEventType.OwnGoal when matchEvent.TournamentTeamId == match.AwayTournamentTeamId => match.HomeTournamentTeamId,
            _ => null,
        };
    }
}

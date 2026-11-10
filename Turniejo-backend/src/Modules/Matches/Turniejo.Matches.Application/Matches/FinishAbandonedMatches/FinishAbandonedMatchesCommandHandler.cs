using System.Text.Json;
using Turniejo.Matches.Application.Abstractions;
using Turniejo.Matches.Domain.Entities;
using Turniejo.Matches.Domain.Enums;
using Turniejo.SharedKernel.Contracts;
using Turniejo.SharedKernel.CQRS;
using Turniejo.SharedKernel.Outbox;
using Turniejo.SharedKernel.Results;

namespace Turniejo.Matches.Application.Matches.FinishAbandonedMatches
{
    /// <summary>
    /// Mecz, którego nikt nie zakończył, trwał w nieskończoność (zegar pokazywał tysiące minut),
    /// a tabela i drabinka czekały na wynik. Po 3× czasie meczu od startu (min. 2 h, zapas na
    /// pauzy, dogrywkę i karne) kończymy go z bieżącym wynikiem. Remisu w pucharze nie da się
    /// zakończyć bez karnych, więc system raz go wstrzymuje i powiadamia organizatora; od tej
    /// chwili mecz należy do organizatora i system go nie rusza.
    /// </summary>
    internal sealed class FinishAbandonedMatchesCommandHandler(
        IMatchesDbContext dbContext,
        ITournamentModule tournamentModule) : ICommandHandler<FinishAbandonedMatchesCommand, FinishAbandonedMatchesResponse>
    {
        public static readonly TimeSpan MinimumThreshold = TimeSpan.FromHours(2);
        private const int DurationMultiplier = 3;

        public async Task<Result<FinishAbandonedMatchesResponse>> Handle(FinishAbandonedMatchesCommand request, CancellationToken cancellationToken)
        {
            var candidates = await dbContext.GetTrackedUnfinishedMatchesStartedBeforeAsync(request.Now - MinimumThreshold, cancellationToken);
            var finished = 0;
            var awaitingDecision = 0;

            foreach (var tournamentMatches in candidates.GroupBy(m => m.TournamentId))
            {
                var settings = await tournamentModule.GetTournamentSettingsAsync(tournamentMatches.Key, cancellationToken);
                if (settings == null)
                    continue;

                var threshold = TimeSpan.FromMinutes(Math.Max(DurationMultiplier * settings.MatchDurationMinutes, MinimumThreshold.TotalMinutes));

                foreach (var match in tournamentMatches)
                {
                    if (match.StartedAt!.Value + threshold > request.Now)
                        continue;
                    if (match.AwaitingDecisionSince.HasValue)
                        continue;

                    if (match.NeedsDecisionToFinish)
                    {
                        if (match.MarkAwaitingDecision(request.Now).IsFailure)
                            continue;
                        await AddOutboxMessagesAsync(match, cancellationToken);
                        awaitingDecision++;
                        continue;
                    }

                    // Guid.Empty = system: nikt z ludzi nie kliknął "Zakończ".
                    var result = match.Finish(MatchResultType.Regular, null, settings.RefereeEditWindowMinutes, Guid.Empty);
                    if (result.IsFailure)
                        continue;

                    var elapsedSeconds = match.GetElapsedSeconds(match.FinishedAt!.Value);
                    var matchEvent = MatchEvent.Create(
                        Guid.NewGuid(), match.Id, MatchEventType.MatchFinished, elapsedSeconds / 60, Guid.Empty,
                        second: elapsedSeconds % 60);
                    if (matchEvent.IsFailure)
                        continue;

                    await dbContext.AddMatchEventAsync(matchEvent.Value, cancellationToken);
                    await AddOutboxMessagesAsync(match, cancellationToken);
                    finished++;
                }
            }

            if (finished + awaitingDecision > 0)
                await dbContext.SaveChangesAsync(cancellationToken);

            return Result.Success(new FinishAbandonedMatchesResponse(finished, awaitingDecision));
        }

        private async Task AddOutboxMessagesAsync(Match match, CancellationToken cancellationToken)
        {
            foreach (var domainEvent in match.DomainEvents)
                await dbContext.AddOutboxMessageAsync(
                    OutboxMessage.Create(domainEvent.GetType().AssemblyQualifiedName!, JsonSerializer.Serialize(domainEvent, domainEvent.GetType())),
                    cancellationToken);
            match.ClearDomainEvents();
        }
    }
}

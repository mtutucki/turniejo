using System.Text.Json;
using Turniejo.Matches.Application.Abstractions;
using Turniejo.Matches.Domain.Entities;
using Turniejo.Matches.Domain.Enums;
using Turniejo.SharedKernel.Contracts;
using Turniejo.SharedKernel.CQRS;
using Turniejo.SharedKernel.Outbox;
using Turniejo.SharedKernel.Results;

namespace Turniejo.Matches.Application.Matches.RecordMatchResult
{
    internal sealed class RecordMatchResultCommandHandler(
        IMatchesDbContext dbContext,
        ITournamentModule tournamentModule,
        ICurrentUserContext currentUserContext) : ICommandHandler<RecordMatchResultCommand, RecordMatchResultResponse>
    {
        public async Task<Result<RecordMatchResultResponse>> Handle(RecordMatchResultCommand request, CancellationToken cancellationToken)
        {
            var match = await dbContext.GetMatchByIdAsync(request.MatchId, cancellationToken);
            if (match == null || match.TournamentId != request.TournamentId)
                return Result.Failure<RecordMatchResultResponse>(Error.NotFound("Match", request.MatchId));

            var settings = await tournamentModule.GetTournamentSettingsAsync(request.TournamentId, cancellationToken);
            if (settings == null)
                return Result.Failure<RecordMatchResultResponse>(Error.NotFound("TournamentSettings", request.TournamentId));

            var userId = currentUserContext.UserId;
            var result = match.RecordResult(
                request.ResultType, request.WinnerTournamentTeamId, request.HomeScore, request.AwayScore,
                settings.RefereeEditWindowMinutes, userId,
                settings.WalkoverWinnerScore, settings.WalkoverLoserScore,
                request.HomePenaltyScore, request.AwayPenaltyScore);
            if (result.IsFailure)
                return Result.Failure<RecordMatchResultResponse>(result.Error);

            // Gole jako zdarzenia bez strzelca: wynik da się potem poprawić i uzupełnić strzelców tak jak
            // po meczu na żywo, a przeliczanie wyniku ze zdarzeń (RecalculateMatchScore) daje to samo.
            // Walkower ma wynik z regulaminu, więc goli nie zapisujemy.
            var events = new List<MatchEvent>();
            if (request.ResultType != MatchResultType.Walkover)
            {
                for (var i = 0; i < request.HomeScore; i++)
                    events.Add(MatchEvent.Create(Guid.NewGuid(), match.Id, MatchEventType.Goal, 0, userId, match.HomeTournamentTeamId).Value);
                for (var i = 0; i < request.AwayScore; i++)
                    events.Add(MatchEvent.Create(Guid.NewGuid(), match.Id, MatchEventType.Goal, 0, userId, match.AwayTournamentTeamId).Value);
            }

            events.Add(MatchEvent.Create(Guid.NewGuid(), match.Id, MatchEventType.MatchFinished, 0, userId).Value);

            foreach (var matchEvent in events)
                await dbContext.AddMatchEventAsync(matchEvent, cancellationToken);

            foreach (var domainEvent in match.DomainEvents)
            {
                var outboxMessage = OutboxMessage.Create(
                    domainEvent.GetType().AssemblyQualifiedName!,
                    JsonSerializer.Serialize(domainEvent, domainEvent.GetType()));

                await dbContext.AddOutboxMessageAsync(outboxMessage, cancellationToken);
            }

            await dbContext.SaveChangesAsync(cancellationToken);

            // Tak jak przy starcie meczu: pierwszy wynik przełącza turniej na "W trakcie".
            await tournamentModule.MarkTournamentStartedAsync(match.TournamentId, userId, cancellationToken);

            return Result.Success(new RecordMatchResultResponse(match.Id, match.HomeScore, match.AwayScore));
        }
    }
}

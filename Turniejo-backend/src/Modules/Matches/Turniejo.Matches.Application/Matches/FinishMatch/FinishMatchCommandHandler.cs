using System.Text.Json;
using Turniejo.Matches.Application.Abstractions;
using Turniejo.Matches.Domain.Entities;
using Turniejo.Matches.Domain.Enums;
using Turniejo.SharedKernel.Contracts;
using Turniejo.SharedKernel.CQRS;
using Turniejo.SharedKernel.Outbox;
using Turniejo.SharedKernel.Results;

namespace Turniejo.Matches.Application.Matches.FinishMatch
{
    internal sealed class FinishMatchCommandHandler(
        IMatchesDbContext dbContext,
        ITournamentModule tournamentModule,
        ICurrentUserContext currentUserContext) : ICommandHandler<FinishMatchCommand, FinishMatchResponse>
    {
        public async Task<Result<FinishMatchResponse>> Handle(FinishMatchCommand request, CancellationToken cancellationToken)
        {
            var match = await dbContext.GetMatchByIdAsync(request.MatchId, cancellationToken);
            if (match == null || match.TournamentId != request.TournamentId)
                return Result.Failure<FinishMatchResponse>(Error.NotFound("Match", request.MatchId));

            var settings = await tournamentModule.GetTournamentSettingsAsync(request.TournamentId, cancellationToken);
            if (settings == null)
                return Result.Failure<FinishMatchResponse>(Error.NotFound("TournamentSettings", request.TournamentId));

            var result = match.Finish(
                request.ResultType, request.WinnerTournamentTeamId, settings.RefereeEditWindowMinutes, currentUserContext.UserId,
                settings.WalkoverWinnerScore, settings.WalkoverLoserScore);
            if (result.IsFailure)
                return Result.Failure<FinishMatchResponse>(result.Error);

            var elapsedSeconds = match.GetElapsedSeconds(match.FinishedAt!.Value);
            var matchEventResult = MatchEvent.Create(
                Guid.NewGuid(), match.Id, MatchEventType.MatchFinished, elapsedSeconds / 60, currentUserContext.UserId,
                second: elapsedSeconds % 60);
            if (matchEventResult.IsFailure)
                return Result.Failure<FinishMatchResponse>(matchEventResult.Error);

            await dbContext.AddMatchEventAsync(matchEventResult.Value, cancellationToken);

            foreach (var domainEvent in match.DomainEvents)
            {
                var outboxMessage = OutboxMessage.Create(
                    domainEvent.GetType().AssemblyQualifiedName!,
                    JsonSerializer.Serialize(domainEvent, domainEvent.GetType()));

                await dbContext.AddOutboxMessageAsync(outboxMessage, cancellationToken);
            }

            await dbContext.SaveChangesAsync(cancellationToken);

            return Result.Success(new FinishMatchResponse(match.Id));
        }
    }
}

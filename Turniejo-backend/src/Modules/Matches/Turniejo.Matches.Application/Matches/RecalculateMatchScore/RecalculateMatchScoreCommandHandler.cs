using System.Text.Json;
using Turniejo.Matches.Application.Abstractions;
using Turniejo.Matches.Domain.Entities;
using Turniejo.Matches.Domain.Enums;
using Turniejo.SharedKernel.CQRS;
using Turniejo.SharedKernel.Outbox;
using Turniejo.SharedKernel.Results;

namespace Turniejo.Matches.Application.Matches.RecalculateMatchScore
{
    internal sealed class RecalculateMatchScoreCommandHandler(IMatchesDbContext dbContext)
        : ICommandHandler<RecalculateMatchScoreCommand, RecalculateMatchScoreResponse>
    {
        public async Task<Result<RecalculateMatchScoreResponse>> Handle(RecalculateMatchScoreCommand request, CancellationToken cancellationToken)
        {
            var match = await dbContext.GetMatchByIdAsync(request.MatchId, cancellationToken);
            if (match == null)
                return Result.Failure<RecalculateMatchScoreResponse>(Error.NotFound("Match", request.MatchId));

            var events = await dbContext.GetMatchEventsByMatchIdAsync(request.MatchId, cancellationToken);

            var homeScore = 0;
            var awayScore = 0;

            foreach (var matchEvent in events)
            {
                if (matchEvent.Type is not (MatchEventType.Goal or MatchEventType.OwnGoal or MatchEventType.PenaltyGoal))
                    continue;

                var scoringTeamId = matchEvent.Type == MatchEventType.OwnGoal
                    ? OpposingTeam(match, matchEvent.TournamentTeamId)
                    : matchEvent.TournamentTeamId;

                if (scoringTeamId == match.HomeTournamentTeamId)
                    homeScore++;
                else if (scoringTeamId == match.AwayTournamentTeamId)
                    awayScore++;
            }

            var result = match.UpdateScore(homeScore, awayScore);
            if (result.IsFailure)
                return Result.Failure<RecalculateMatchScoreResponse>(result.Error);

            foreach (var domainEvent in match.DomainEvents)
            {
                var outboxMessage = OutboxMessage.Create(
                    domainEvent.GetType().AssemblyQualifiedName!,
                    JsonSerializer.Serialize(domainEvent, domainEvent.GetType()));

                await dbContext.AddOutboxMessageAsync(outboxMessage, cancellationToken);
            }

            await dbContext.SaveChangesAsync(cancellationToken);

            return Result.Success(new RecalculateMatchScoreResponse(homeScore, awayScore));
        }

        private static Guid? OpposingTeam(Match match, Guid? teamId)
        {
            if (teamId == match.HomeTournamentTeamId)
                return match.AwayTournamentTeamId;

            if (teamId == match.AwayTournamentTeamId)
                return match.HomeTournamentTeamId;

            return null;
        }
    }
}

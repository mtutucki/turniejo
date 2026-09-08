using System.Text.Json;
using Turniejo.Matches.Application.Abstractions;
using Turniejo.Matches.Domain.Entities;
using Turniejo.Matches.Domain.Enums;
using Turniejo.Matches.Domain.Events;
using Turniejo.SharedKernel.Contracts;
using Turniejo.SharedKernel.CQRS;
using Turniejo.SharedKernel.Outbox;
using Turniejo.SharedKernel.Results;

namespace Turniejo.Matches.Application.Matches.AddAssist
{
    internal sealed class AddAssistCommandHandler(
        IMatchesDbContext dbContext,
        ICurrentUserContext currentUserContext) : ICommandHandler<AddAssistCommand, AddAssistResponse>
    {
        public async Task<Result<AddAssistResponse>> Handle(AddAssistCommand request, CancellationToken cancellationToken)
        {
            var match = await dbContext.GetMatchByIdAsync(request.MatchId, cancellationToken);
            if (match == null || match.TournamentId != request.TournamentId)
                return Result.Failure<AddAssistResponse>(Error.NotFound("Match", request.MatchId));

            var editableCheck = match.EnsureEditable();
            if (editableCheck.IsFailure)
                return Result.Failure<AddAssistResponse>(editableCheck.Error);

            if (request.TournamentTeamId != match.HomeTournamentTeamId && request.TournamentTeamId != match.AwayTournamentTeamId)
                return Result.Failure<AddAssistResponse>(Error.Validation("TournamentTeamId", "Team is not playing in this match"));

            var eventResult = MatchEvent.Create(
                Guid.NewGuid(), match.Id, MatchEventType.Assist, request.Minute, currentUserContext.UserId,
                request.TournamentTeamId, request.PlayerId, request.ScorerPlayerId, request.Second);
            if (eventResult.IsFailure)
                return Result.Failure<AddAssistResponse>(eventResult.Error);

            await dbContext.AddMatchEventAsync(eventResult.Value, cancellationToken);

            var addedEvent = new MatchEventAdded(eventResult.Value.Id, match.Id, match.TournamentId, MatchEventType.Assist.ToString());
            await dbContext.AddOutboxMessageAsync(
                OutboxMessage.Create(addedEvent.GetType().AssemblyQualifiedName!, JsonSerializer.Serialize(addedEvent, addedEvent.GetType())),
                cancellationToken);

            await dbContext.SaveChangesAsync(cancellationToken);

            return Result.Success(new AddAssistResponse(eventResult.Value.Id));
        }
    }
}

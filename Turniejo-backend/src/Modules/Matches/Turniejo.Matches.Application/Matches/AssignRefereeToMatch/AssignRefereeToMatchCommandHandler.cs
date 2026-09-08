using System.Text.Json;
using Turniejo.Matches.Application.Abstractions;
using Turniejo.SharedKernel.Contracts;
using Turniejo.SharedKernel.CQRS;
using Turniejo.SharedKernel.Enums;
using Turniejo.SharedKernel.Outbox;
using Turniejo.SharedKernel.Results;

namespace Turniejo.Matches.Application.Matches.AssignRefereeToMatch
{
    internal sealed class AssignRefereeToMatchCommandHandler(
        IMatchesDbContext dbContext,
        IUserModule userModule,
        ICurrentUserContext currentUserContext) : ICommandHandler<AssignRefereeToMatchCommand, AssignRefereeToMatchResponse>
    {
        public async Task<Result<AssignRefereeToMatchResponse>> Handle(AssignRefereeToMatchCommand request, CancellationToken cancellationToken)
        {
            var match = await dbContext.GetMatchByIdAsync(request.MatchId, cancellationToken);
            if (match == null || match.TournamentId != request.TournamentId)
                return Result.Failure<AssignRefereeToMatchResponse>(Error.NotFound("Match", request.MatchId));

            var result = match.AssignReferee(request.RefereeId, currentUserContext.UserId);
            if (result.IsFailure)
                return Result.Failure<AssignRefereeToMatchResponse>(result.Error);

            if (request.RefereeId.HasValue)
            {
                await userModule.AssignRoleAsync(
                    request.RefereeId.Value,
                    nameof(UserRole.Referee),
                    nameof(RoleScopeType.Match),
                    match.Id,
                    cancellationToken);
            }

            foreach (var domainEvent in match.DomainEvents)
            {
                var outboxMessage = OutboxMessage.Create(
                    domainEvent.GetType().AssemblyQualifiedName!,
                    JsonSerializer.Serialize(domainEvent, domainEvent.GetType()));

                await dbContext.AddOutboxMessageAsync(outboxMessage, cancellationToken);
            }

            await dbContext.SaveChangesAsync(cancellationToken);

            return Result.Success(new AssignRefereeToMatchResponse(match.Id));
        }
    }
}

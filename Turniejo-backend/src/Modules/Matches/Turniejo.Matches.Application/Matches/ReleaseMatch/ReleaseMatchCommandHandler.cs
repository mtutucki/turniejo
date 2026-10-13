using System.Text.Json;
using Turniejo.Matches.Application.Abstractions;
using Turniejo.SharedKernel.Contracts;
using Turniejo.SharedKernel.CQRS;
using Turniejo.SharedKernel.Enums;
using Turniejo.SharedKernel.Outbox;
using Turniejo.SharedKernel.Results;

namespace Turniejo.Matches.Application.Matches.ReleaseMatch
{
    internal sealed class ReleaseMatchCommandHandler(
        IMatchesDbContext dbContext,
        IUserModule userModule,
        ICurrentUserContext currentUserContext) : ICommandHandler<ReleaseMatchCommand, ReleaseMatchResponse>
    {
        public async Task<Result<ReleaseMatchResponse>> Handle(ReleaseMatchCommand request, CancellationToken cancellationToken)
        {
            var match = await dbContext.GetMatchByIdAsync(request.MatchId, cancellationToken);
            if (match == null || match.TournamentId != request.TournamentId)
                return Result.Failure<ReleaseMatchResponse>(Error.NotFound("Match", request.MatchId));

            var refereeId = currentUserContext.UserId;

            var result = match.ReleaseReferee(refereeId);
            if (result.IsFailure)
                return Result.Failure<ReleaseMatchResponse>(result.Error);

            foreach (var domainEvent in match.DomainEvents)
            {
                var outboxMessage = OutboxMessage.Create(
                    domainEvent.GetType().AssemblyQualifiedName!,
                    JsonSerializer.Serialize(domainEvent, domainEvent.GetType()));

                await dbContext.AddOutboxMessageAsync(outboxMessage, cancellationToken);
            }

            await dbContext.SaveChangesAsync(cancellationToken);

            // Bez tego sędzia po oddaniu meczu dalej miałby do niego panel (start/pauza/eventy).
            await userModule.RevokeRoleAsync(
                refereeId,
                nameof(UserRole.Referee),
                nameof(RoleScopeType.Match),
                match.Id,
                cancellationToken);

            return Result.Success(new ReleaseMatchResponse(match.Id));
        }
    }
}

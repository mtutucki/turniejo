using System.Text.Json;
using Turniejo.Players.Application.Abstractions;
using Turniejo.Players.Domain.Entities;
using Turniejo.SharedKernel.Contracts;
using Turniejo.SharedKernel.CQRS;
using Turniejo.SharedKernel.Enums;
using Turniejo.SharedKernel.Outbox;
using Turniejo.SharedKernel.Results;

namespace Turniejo.Players.Application.PlayerGuardians.AssignGuardianToPlayer
{
    internal sealed class AssignGuardianToPlayerCommandHandler(
        IPlayersDbContext dbContext,
        IUserModule userModule,
        ICurrentUserContext currentUserContext) : ICommandHandler<AssignGuardianToPlayerCommand, AssignGuardianToPlayerResponse>
    {
        public async Task<Result<AssignGuardianToPlayerResponse>> Handle(AssignGuardianToPlayerCommand request, CancellationToken cancellationToken)
        {
            var player = await dbContext.GetPlayerByIdAsync(request.PlayerId, cancellationToken);
            if (player == null)
                return Result.Failure<AssignGuardianToPlayerResponse>(Error.NotFound("Player", request.PlayerId));

            var alreadyGuardian = await dbContext.PlayerGuardianExistsAsync(request.PlayerId, request.UserId, cancellationToken);
            if (alreadyGuardian)
                return Result.Failure<AssignGuardianToPlayerResponse>(Error.Validation("UserId", "User is already a guardian of this player"));

            var result = PlayerGuardian.Create(
                Guid.NewGuid(),
                request.PlayerId,
                request.UserId,
                request.RelationshipType,
                request.CanEdit,
                request.CanReceiveNotifications,
                currentUserContext.UserId);

            if (result.IsFailure)
                return Result.Failure<AssignGuardianToPlayerResponse>(result.Error);

            var playerGuardian = result.Value;

            await dbContext.AddPlayerGuardianAsync(playerGuardian, cancellationToken);

            await userModule.AssignRoleAsync(
                request.UserId,
                nameof(UserRole.Parent),
                nameof(RoleScopeType.Player),
                request.PlayerId,
                cancellationToken);

            foreach (var domainEvent in playerGuardian.DomainEvents)
            {
                var outboxMessage = OutboxMessage.Create(
                    domainEvent.GetType().AssemblyQualifiedName!,
                    JsonSerializer.Serialize(domainEvent, domainEvent.GetType()));

                await dbContext.AddOutboxMessageAsync(outboxMessage, cancellationToken);
            }

            await dbContext.SaveChangesAsync(cancellationToken);

            return Result.Success(new AssignGuardianToPlayerResponse(playerGuardian.Id));
        }
    }
}

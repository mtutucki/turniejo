using System.Text.Json;
using Turniejo.Players.Application.Abstractions;
using Turniejo.Players.Domain.Entities;
using Turniejo.SharedKernel.Contracts;
using Turniejo.SharedKernel.CQRS;
using Turniejo.SharedKernel.Outbox;
using Turniejo.SharedKernel.Results;

namespace Turniejo.Players.Application.Players.CreatePlayer
{
    internal sealed class CreatePlayerCommandHandler(IPlayersDbContext dbContext, ICurrentUserContext currentUserContext) : ICommandHandler<CreatePlayerCommand, CreatePlayerResponse>
    {
        public async Task<Result<CreatePlayerResponse>> Handle(CreatePlayerCommand request, CancellationToken cancellationToken)
        {
            var result = Player.Create(
                Guid.NewGuid(),
                request.FirstName,
                request.LastName,
                request.BirthDate,
                request.DefaultPosition,
                request.PhotoUrl,
                request.UserId,
                currentUserContext.UserId);

            if (result.IsFailure)
                return Result.Failure<CreatePlayerResponse>(result.Error);

            var player = result.Value;

            await dbContext.AddPlayerAsync(player, cancellationToken);

            foreach (var domainEvent in player.DomainEvents)
            {
                var outboxMessage = OutboxMessage.Create(
                    domainEvent.GetType().AssemblyQualifiedName!,
                    JsonSerializer.Serialize(domainEvent, domainEvent.GetType()));

                await dbContext.AddOutboxMessageAsync(outboxMessage, cancellationToken);
            }

            await dbContext.SaveChangesAsync(cancellationToken);

            return Result.Success(new CreatePlayerResponse(player.Id));
        }
    }
}

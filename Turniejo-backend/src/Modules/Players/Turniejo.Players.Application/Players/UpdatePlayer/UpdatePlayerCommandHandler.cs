using System.Text.Json;
using Turniejo.Players.Application.Abstractions;
using Turniejo.Players.Application.Services;
using Turniejo.SharedKernel.Contracts;
using Turniejo.SharedKernel.CQRS;
using Turniejo.SharedKernel.Exceptions;
using Turniejo.SharedKernel.Outbox;
using Turniejo.SharedKernel.Results;

namespace Turniejo.Players.Application.Players.UpdatePlayer
{
    internal sealed class UpdatePlayerCommandHandler(
        IPlayersDbContext dbContext,
        PlayerAccessGuard playerAccessGuard,
        ICurrentUserContext currentUserContext) : ICommandHandler<UpdatePlayerCommand, UpdatePlayerResponse>
    {
        public async Task<Result<UpdatePlayerResponse>> Handle(UpdatePlayerCommand request, CancellationToken cancellationToken)
        {
            var player = await dbContext.GetPlayerByIdAsync(request.PlayerId, cancellationToken);
            if (player == null)
                return Result.Failure<UpdatePlayerResponse>(Error.NotFound("Player", request.PlayerId));

            // Policy Parent/Coach/Organizer: fail-closed — brak dopasowanej relacji (nawet gdy zawodnik
            // nie ma jeszcze żadnego opiekuna) oznacza brak dostępu, nie otwartą edycję.
            if (!await playerAccessGuard.CanEditAsync(player, currentUserContext.UserId, cancellationToken))
                throw new UnauthorizedException();

            var updateResult = player.UpdateDetails(request.FirstName, request.LastName, request.BirthDate, request.DefaultPosition, request.PhotoUrl);
            if (updateResult.IsFailure)
                return Result.Failure<UpdatePlayerResponse>(updateResult.Error);

            foreach (var domainEvent in player.DomainEvents)
            {
                var outboxMessage = OutboxMessage.Create(
                    domainEvent.GetType().AssemblyQualifiedName!,
                    JsonSerializer.Serialize(domainEvent, domainEvent.GetType()));

                await dbContext.AddOutboxMessageAsync(outboxMessage, cancellationToken);
            }

            await dbContext.SaveChangesAsync(cancellationToken);

            return Result.Success(new UpdatePlayerResponse(player.Id));
        }
    }
}

using System.Text.Json;
using Turniejo.Players.Application.Abstractions;
using Turniejo.SharedKernel.CQRS;
using Turniejo.SharedKernel.Outbox;
using Turniejo.SharedKernel.Results;

namespace Turniejo.Players.Application.TournamentPlayers.UpdateTournamentPlayer
{
    internal sealed class UpdateTournamentPlayerCommandHandler(IPlayersDbContext dbContext) : ICommandHandler<UpdateTournamentPlayerCommand, UpdateTournamentPlayerResponse>
    {
        public async Task<Result<UpdateTournamentPlayerResponse>> Handle(UpdateTournamentPlayerCommand request, CancellationToken cancellationToken)
        {
            var tournamentPlayer = await dbContext.GetTournamentPlayerByIdAsync(request.TournamentPlayerId, cancellationToken);
            if (tournamentPlayer == null || tournamentPlayer.TournamentTeamId != request.TournamentTeamId)
                return Result.Failure<UpdateTournamentPlayerResponse>(Error.NotFound("TournamentPlayer", request.TournamentPlayerId));

            if (request.ShirtNumber.HasValue && request.ShirtNumber != tournamentPlayer.ShirtNumber)
            {
                var shirtNumberTaken = await dbContext.ShirtNumberExistsAsync(request.TournamentTeamId, request.ShirtNumber.Value, cancellationToken);
                if (shirtNumberTaken)
                    return Result.Failure<UpdateTournamentPlayerResponse>(Error.Validation("ShirtNumber", "Shirt number is already taken in this team"));
            }

            var result = tournamentPlayer.UpdateDetails(request.ShirtNumber, request.Position, request.Status);
            if (result.IsFailure)
                return Result.Failure<UpdateTournamentPlayerResponse>(result.Error);

            foreach (var domainEvent in tournamentPlayer.DomainEvents)
            {
                var outboxMessage = OutboxMessage.Create(
                    domainEvent.GetType().AssemblyQualifiedName!,
                    JsonSerializer.Serialize(domainEvent, domainEvent.GetType()));

                await dbContext.AddOutboxMessageAsync(outboxMessage, cancellationToken);
            }

            await dbContext.SaveChangesAsync(cancellationToken);

            return Result.Success(new UpdateTournamentPlayerResponse(tournamentPlayer.Id));
        }
    }
}

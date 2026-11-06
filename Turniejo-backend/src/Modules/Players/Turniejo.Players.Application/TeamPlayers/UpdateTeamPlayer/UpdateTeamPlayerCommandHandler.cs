using System.Text.Json;
using Turniejo.Players.Application.Abstractions;
using Turniejo.Players.Application.Services;
using Turniejo.SharedKernel.Contracts;
using Turniejo.SharedKernel.CQRS;
using Turniejo.SharedKernel.Exceptions;
using Turniejo.SharedKernel.Outbox;
using Turniejo.SharedKernel.Results;

namespace Turniejo.Players.Application.TeamPlayers.UpdateTeamPlayer
{
    internal sealed class UpdateTeamPlayerCommandHandler(
        IPlayersDbContext dbContext,
        TeamRosterAccessGuard accessGuard,
        ICurrentUserContext currentUserContext) : ICommandHandler<UpdateTeamPlayerCommand, UpdateTeamPlayerResponse>
    {
        public async Task<Result<UpdateTeamPlayerResponse>> Handle(UpdateTeamPlayerCommand request, CancellationToken cancellationToken)
        {
            var access = await accessGuard.GetAccessAsync(request.TeamId, currentUserContext.UserId, cancellationToken);
            if (access != TeamRosterAccess.Edit)
                throw new UnauthorizedException();

            var member = await dbContext.GetTeamPlayerAsync(request.TeamId, request.PlayerId, cancellationToken);
            var player = member == null ? null : await dbContext.GetPlayerByIdAsync(request.PlayerId, cancellationToken);
            if (member == null || player == null)
                return Result.Failure<UpdateTeamPlayerResponse>(Error.NotFound("Player", request.PlayerId));

            var members = await dbContext.GetTeamPlayersByTeamIdAsync(request.TeamId, cancellationToken);
            if (TeamRosterRules.IsShirtNumberTaken(members, request.ShirtNumber, exceptPlayerId: request.PlayerId))
                return Result.Failure<UpdateTeamPlayerResponse>(Error.Validation("ShirtNumber", "Shirt number is already taken in this team"));

            var profileResult = player.UpdateDetails(
                request.FirstName.Trim(), request.LastName.Trim(), request.BirthDate, request.DefaultPosition, player.PhotoUrl);
            if (profileResult.IsFailure)
                return Result.Failure<UpdateTeamPlayerResponse>(profileResult.Error);

            var numberResult = member.UpdateShirtNumber(request.ShirtNumber);
            if (numberResult.IsFailure)
                return Result.Failure<UpdateTeamPlayerResponse>(numberResult.Error);

            foreach (var domainEvent in player.DomainEvents)
            {
                await dbContext.AddOutboxMessageAsync(OutboxMessage.Create(
                    domainEvent.GetType().AssemblyQualifiedName!,
                    JsonSerializer.Serialize(domainEvent, domainEvent.GetType())), cancellationToken);
            }

            await dbContext.SaveChangesAsync(cancellationToken);
            return Result.Success(new UpdateTeamPlayerResponse(player.Id));
        }
    }
}

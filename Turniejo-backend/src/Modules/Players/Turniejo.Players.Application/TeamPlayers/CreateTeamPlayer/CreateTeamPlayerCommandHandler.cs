using System.Text.Json;
using Turniejo.Players.Application.Abstractions;
using Turniejo.Players.Application.Services;
using Turniejo.Players.Domain.Entities;
using Turniejo.SharedKernel.Contracts;
using Turniejo.SharedKernel.CQRS;
using Turniejo.SharedKernel.Exceptions;
using Turniejo.SharedKernel.Outbox;
using Turniejo.SharedKernel.Results;

namespace Turniejo.Players.Application.TeamPlayers.CreateTeamPlayer
{
    internal sealed class CreateTeamPlayerCommandHandler(
        IPlayersDbContext dbContext,
        TeamRosterAccessGuard accessGuard,
        ICurrentUserContext currentUserContext) : ICommandHandler<CreateTeamPlayerCommand, CreateTeamPlayerResponse>
    {
        public async Task<Result<CreateTeamPlayerResponse>> Handle(CreateTeamPlayerCommand request, CancellationToken cancellationToken)
        {
            var access = await accessGuard.GetAccessAsync(request.TeamId, currentUserContext.UserId, cancellationToken);
            if (access != TeamRosterAccess.Edit)
                throw new UnauthorizedException();

            var members = await dbContext.GetTeamPlayersByTeamIdAsync(request.TeamId, cancellationToken);

            if (members.Count > 0 && !request.AllowDuplicate)
            {
                var profiles = await dbContext.GetPlayersByIdsAsync(members.Select(m => m.PlayerId).ToList(), cancellationToken);
                if (TeamRosterRules.HasNameTwin(profiles, request.FirstName, request.LastName, request.BirthDate))
                    return Result.Failure<CreateTeamPlayerResponse>(Error.Validation("LastName", "A player with the same name and birth date is already in this team"));
            }

            if (TeamRosterRules.IsShirtNumberTaken(members, request.ShirtNumber, exceptPlayerId: null))
                return Result.Failure<CreateTeamPlayerResponse>(Error.Validation("ShirtNumber", "Shirt number is already taken in this team"));

            var playerResult = Player.Create(
                Guid.NewGuid(), request.FirstName.Trim(), request.LastName.Trim(), request.BirthDate,
                request.DefaultPosition, null, null, currentUserContext.UserId);
            if (playerResult.IsFailure)
                return Result.Failure<CreateTeamPlayerResponse>(playerResult.Error);

            var player = playerResult.Value;
            var memberResult = TeamPlayer.Create(Guid.NewGuid(), request.TeamId, player.Id, request.ShirtNumber);
            if (memberResult.IsFailure)
                return Result.Failure<CreateTeamPlayerResponse>(memberResult.Error);

            await dbContext.AddPlayerAsync(player, cancellationToken);
            await dbContext.AddTeamPlayerAsync(memberResult.Value, cancellationToken);

            foreach (var domainEvent in player.DomainEvents)
            {
                await dbContext.AddOutboxMessageAsync(OutboxMessage.Create(
                    domainEvent.GetType().AssemblyQualifiedName!,
                    JsonSerializer.Serialize(domainEvent, domainEvent.GetType())), cancellationToken);
            }

            // Jeden SaveChanges: profil powstaje tylko razem z wpisem w kadrze.
            await dbContext.SaveChangesAsync(cancellationToken);
            return Result.Success(new CreateTeamPlayerResponse(player.Id));
        }
    }
}

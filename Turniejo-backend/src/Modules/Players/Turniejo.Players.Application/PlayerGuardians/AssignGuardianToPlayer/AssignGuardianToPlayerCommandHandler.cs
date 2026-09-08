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
        ITeamModule teamModule,
        IUserModule userModule,
        ICurrentUserContext currentUserContext) : ICommandHandler<AssignGuardianToPlayerCommand, AssignGuardianToPlayerResponse>
    {
        public async Task<Result<AssignGuardianToPlayerResponse>> Handle(AssignGuardianToPlayerCommand request, CancellationToken cancellationToken)
        {
            // AuthorizationBehavior już potwierdził, że wywołujący jest Organizerem request.TournamentId
            // albo Coachem request.TournamentTeamId — ale oba te ID pochodzą od klienta, więc trzeba
            // jeszcze sprawdzić, że faktycznie do siebie pasują i że PlayerId naprawdę jest w tym składzie
            // (inaczej Organizer dowolnego turnieju mógłby wskazać PlayerId spoza swojego zasięgu).
            var tournamentTeam = await teamModule.GetTournamentTeamAsync(request.TournamentTeamId, cancellationToken);
            if (tournamentTeam == null || tournamentTeam.TournamentId != request.TournamentId)
                return Result.Failure<AssignGuardianToPlayerResponse>(Error.NotFound("TournamentTeam", request.TournamentTeamId));

            var isOnRoster = await dbContext.TournamentPlayerExistsAsync(request.TournamentTeamId, request.PlayerId, cancellationToken);
            if (!isOnRoster)
                return Result.Failure<AssignGuardianToPlayerResponse>(Error.NotFound("Player", request.PlayerId));

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

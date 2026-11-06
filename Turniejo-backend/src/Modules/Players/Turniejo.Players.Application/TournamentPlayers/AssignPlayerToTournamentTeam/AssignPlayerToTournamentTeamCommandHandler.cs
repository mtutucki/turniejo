using System.Text.Json;
using Turniejo.Players.Application.Abstractions;
using Turniejo.Players.Application.Services;
using Turniejo.Players.Domain.Entities;
using Turniejo.SharedKernel.Contracts;
using Turniejo.SharedKernel.CQRS;
using Turniejo.SharedKernel.Outbox;
using Turniejo.SharedKernel.Results;

namespace Turniejo.Players.Application.TournamentPlayers.AssignPlayerToTournamentTeam
{
    internal sealed class AssignPlayerToTournamentTeamCommandHandler(
        IPlayersDbContext dbContext,
        ITeamModule teamModule,
        ITournamentModule tournamentModule,
        SquadEditGuard squadEditGuard,
        TeamRosterSync teamRosterSync,
        ICurrentUserContext currentUserContext) : ICommandHandler<AssignPlayerToTournamentTeamCommand, AssignPlayerToTournamentTeamResponse>
    {
        public async Task<Result<AssignPlayerToTournamentTeamResponse>> Handle(AssignPlayerToTournamentTeamCommand request, CancellationToken cancellationToken)
        {
            var tournamentTeam = await teamModule.GetTournamentTeamAsync(request.TournamentTeamId, cancellationToken);
            if (tournamentTeam == null || tournamentTeam.TournamentId != request.TournamentId)
                return Result.Failure<AssignPlayerToTournamentTeamResponse>(Error.NotFound("TournamentTeam", request.TournamentTeamId));

            await squadEditGuard.EnsureCanEditSquadAsync(request.TournamentId, currentUserContext.UserId, cancellationToken);

            var player = await dbContext.GetPlayerByIdAsync(request.PlayerId, cancellationToken);
            if (player == null)
                return Result.Failure<AssignPlayerToTournamentTeamResponse>(Error.NotFound("Player", request.PlayerId));

            var category = await tournamentModule.GetTournamentCategoryAsync(tournamentTeam.CategoryId, cancellationToken);
            if (category != null)
            {
                if (category.MinBirthDate.HasValue && player.BirthDate < category.MinBirthDate.Value)
                    return Result.Failure<AssignPlayerToTournamentTeamResponse>(Error.Validation("PlayerId", "Player is too old for this category"));

                if (category.MaxBirthDate.HasValue && player.BirthDate > category.MaxBirthDate.Value)
                    return Result.Failure<AssignPlayerToTournamentTeamResponse>(Error.Validation("PlayerId", "Player is too young for this category"));
            }

            var alreadyAssigned = await dbContext.TournamentPlayerExistsAsync(request.TournamentTeamId, request.PlayerId, cancellationToken);
            if (alreadyAssigned)
                return Result.Failure<AssignPlayerToTournamentTeamResponse>(Error.Validation("PlayerId", "Player is already assigned to this team"));

            if (request.ShirtNumber.HasValue)
            {
                var shirtNumberTaken = await dbContext.ShirtNumberExistsAsync(request.TournamentTeamId, request.ShirtNumber.Value, cancellationToken);
                if (shirtNumberTaken)
                    return Result.Failure<AssignPlayerToTournamentTeamResponse>(Error.Validation("ShirtNumber", "Shirt number is already taken in this team"));
            }

            var result = TournamentPlayer.Create(
                Guid.NewGuid(),
                request.TournamentTeamId,
                request.PlayerId,
                request.ShirtNumber,
                request.Position,
                currentUserContext.UserId);

            if (result.IsFailure)
                return Result.Failure<AssignPlayerToTournamentTeamResponse>(result.Error);

            var tournamentPlayer = result.Value;

            await dbContext.AddTournamentPlayerAsync(tournamentPlayer, cancellationToken);

            var rosterResult = await teamRosterSync.EnsureInRosterAsync(
                tournamentTeam.TeamId, [(request.PlayerId, request.ShirtNumber)], cancellationToken);
            if (rosterResult.IsFailure)
                return Result.Failure<AssignPlayerToTournamentTeamResponse>(rosterResult.Error);

            foreach (var domainEvent in tournamentPlayer.DomainEvents)
            {
                var outboxMessage = OutboxMessage.Create(
                    domainEvent.GetType().AssemblyQualifiedName!,
                    JsonSerializer.Serialize(domainEvent, domainEvent.GetType()));

                await dbContext.AddOutboxMessageAsync(outboxMessage, cancellationToken);
            }

            await dbContext.SaveChangesAsync(cancellationToken);

            return Result.Success(new AssignPlayerToTournamentTeamResponse(tournamentPlayer.Id));
        }
    }
}

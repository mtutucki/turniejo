using System.Text.Json;
using Turniejo.Players.Application.Abstractions;
using Turniejo.Players.Application.Services;
using Turniejo.Players.Domain.Entities;
using Turniejo.SharedKernel.Contracts;
using Turniejo.SharedKernel.CQRS;
using Turniejo.SharedKernel.Outbox;
using Turniejo.SharedKernel.Results;

namespace Turniejo.Players.Application.TournamentPlayers.CreatePlayerInTournamentTeam
{
    internal sealed class CreatePlayerInTournamentTeamCommandHandler(
        IPlayersDbContext dbContext,
        ITeamModule teamModule,
        ITournamentModule tournamentModule,
        SquadEditGuard squadEditGuard,
        ICurrentUserContext currentUserContext) : ICommandHandler<CreatePlayerInTournamentTeamCommand, CreatePlayerInTournamentTeamResponse>
    {
        public async Task<Result<CreatePlayerInTournamentTeamResponse>> Handle(CreatePlayerInTournamentTeamCommand request, CancellationToken cancellationToken)
        {
            var tournamentTeam = await teamModule.GetTournamentTeamAsync(request.TournamentTeamId, cancellationToken);
            if (tournamentTeam == null || tournamentTeam.TournamentId != request.TournamentId)
                return Result.Failure<CreatePlayerInTournamentTeamResponse>(Error.NotFound("TournamentTeam", request.TournamentTeamId));

            await squadEditGuard.EnsureCanEditSquadAsync(request.TournamentId, currentUserContext.UserId, cancellationToken);

            var category = await tournamentModule.GetTournamentCategoryAsync(tournamentTeam.CategoryId, cancellationToken);
            if (category != null)
            {
                if (category.MinBirthDate.HasValue && request.BirthDate < category.MinBirthDate.Value)
                    return Result.Failure<CreatePlayerInTournamentTeamResponse>(Error.Validation("BirthDate", "Player is too old for this category"));

                if (category.MaxBirthDate.HasValue && request.BirthDate > category.MaxBirthDate.Value)
                    return Result.Failure<CreatePlayerInTournamentTeamResponse>(Error.Validation("BirthDate", "Player is too young for this category"));
            }

            // Nowy profil nie może być "już w składzie" po id, więc duplikat łapiemy po imieniu, nazwisku i dacie urodzenia.
            // Po potwierdzeniu (AllowDuplicate) dodajemy: dwie różne osoby mogą mieć te same dane.
            var squad = await dbContext.GetTournamentPlayersByTournamentTeamIdAsync(request.TournamentTeamId, cancellationToken);
            if (squad.Count > 0 && !request.AllowDuplicate)
            {
                var squadPlayers = await dbContext.GetPlayersByIdsAsync(squad.Select(tp => tp.PlayerId).ToList(), cancellationToken);
                var duplicate = squadPlayers.Any(p =>
                    string.Equals(p.FirstName.Trim(), request.FirstName.Trim(), StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(p.LastName.Trim(), request.LastName.Trim(), StringComparison.OrdinalIgnoreCase) &&
                    p.BirthDate.Date == request.BirthDate.Date);

                if (duplicate)
                    return Result.Failure<CreatePlayerInTournamentTeamResponse>(Error.Validation("LastName", "A player with the same name and birth date is already in this team"));
            }

            if (request.ShirtNumber.HasValue)
            {
                var shirtNumberTaken = await dbContext.ShirtNumberExistsAsync(request.TournamentTeamId, request.ShirtNumber.Value, cancellationToken);
                if (shirtNumberTaken)
                    return Result.Failure<CreatePlayerInTournamentTeamResponse>(Error.Validation("ShirtNumber", "Shirt number is already taken in this team"));
            }

            var playerResult = Player.Create(
                Guid.NewGuid(),
                request.FirstName,
                request.LastName,
                request.BirthDate,
                request.DefaultPosition,
                request.PhotoUrl,
                null,
                currentUserContext.UserId);

            if (playerResult.IsFailure)
                return Result.Failure<CreatePlayerInTournamentTeamResponse>(playerResult.Error);

            var player = playerResult.Value;

            var tournamentPlayerResult = TournamentPlayer.Create(
                Guid.NewGuid(),
                request.TournamentTeamId,
                player.Id,
                request.ShirtNumber,
                request.Position,
                currentUserContext.UserId);

            if (tournamentPlayerResult.IsFailure)
                return Result.Failure<CreatePlayerInTournamentTeamResponse>(tournamentPlayerResult.Error);

            var tournamentPlayer = tournamentPlayerResult.Value;

            await dbContext.AddPlayerAsync(player, cancellationToken);
            await dbContext.AddTournamentPlayerAsync(tournamentPlayer, cancellationToken);

            foreach (var domainEvent in player.DomainEvents.Concat(tournamentPlayer.DomainEvents))
            {
                var outboxMessage = OutboxMessage.Create(
                    domainEvent.GetType().AssemblyQualifiedName!,
                    JsonSerializer.Serialize(domainEvent, domainEvent.GetType()));

                await dbContext.AddOutboxMessageAsync(outboxMessage, cancellationToken);
            }

            // Jeden SaveChanges = jedna transakcja: albo powstaje profil razem z wpisem w składzie, albo nic.
            await dbContext.SaveChangesAsync(cancellationToken);

            return Result.Success(new CreatePlayerInTournamentTeamResponse(player.Id, tournamentPlayer.Id));
        }
    }
}

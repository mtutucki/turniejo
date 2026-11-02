using System.Text.Json;
using Turniejo.Players.Application.Abstractions;
using Turniejo.Players.Application.Services;
using Turniejo.SharedKernel.Contracts;
using Turniejo.SharedKernel.CQRS;
using Turniejo.SharedKernel.Outbox;
using Turniejo.SharedKernel.Results;

namespace Turniejo.Players.Application.TournamentPlayers.UpdatePlayerInTournamentTeam
{
    internal sealed class UpdatePlayerInTournamentTeamCommandHandler(
        IPlayersDbContext dbContext,
        ITeamModule teamModule,
        ITournamentModule tournamentModule,
        SquadEditGuard squadEditGuard,
        ICurrentUserContext currentUserContext) : ICommandHandler<UpdatePlayerInTournamentTeamCommand, UpdatePlayerInTournamentTeamResponse>
    {
        public async Task<Result<UpdatePlayerInTournamentTeamResponse>> Handle(UpdatePlayerInTournamentTeamCommand request, CancellationToken cancellationToken)
        {
            var tournamentPlayer = await dbContext.GetTournamentPlayerByIdAsync(request.TournamentPlayerId, cancellationToken);
            if (tournamentPlayer == null || tournamentPlayer.TournamentTeamId != request.TournamentTeamId)
                return Result.Failure<UpdatePlayerInTournamentTeamResponse>(Error.NotFound("TournamentPlayer", request.TournamentPlayerId));

            // Pipeline weryfikuje tylko jeden z zakresów (Organizer@Tournament albo Coach@TournamentTeam),
            // więc para turniej–drużyna musi się zgadzać z relacją po stronie serwera: bez tego organizator
            // turnieju A edytowałby zawodników drużyny z turnieju B.
            var tournamentTeam = await teamModule.GetTournamentTeamAsync(tournamentPlayer.TournamentTeamId, cancellationToken);
            if (tournamentTeam == null || tournamentTeam.TournamentId != request.TournamentId)
                return Result.Failure<UpdatePlayerInTournamentTeamResponse>(Error.NotFound("TournamentTeam", request.TournamentTeamId));

            await squadEditGuard.EnsureCanEditSquadAsync(tournamentTeam.TournamentId, currentUserContext.UserId, cancellationToken);

            var player = await dbContext.GetPlayerByIdAsync(tournamentPlayer.PlayerId, cancellationToken);
            if (player == null)
                return Result.Failure<UpdatePlayerInTournamentTeamResponse>(Error.NotFound("Player", tournamentPlayer.PlayerId));

            // Tylko przy zmianie daty: zawodnik, który już jest w składzie poza limitem (np. po zmianie
            // kategorii), nadal musi dać się edytować — numer, status, literówka w nazwisku.
            if (request.BirthDate.Date != player.BirthDate.Date)
            {
                var category = await tournamentModule.GetTournamentCategoryAsync(tournamentTeam.CategoryId, cancellationToken);
                var violation = CategoryAgeRule.Check(category, request.BirthDate);
                if (violation != null)
                    return Result.Failure<UpdatePlayerInTournamentTeamResponse>(Error.Validation("BirthDate", CategoryAgeRule.Message(violation)));
            }

            // Po zmianie danych zawodnik nie może stać się dublem innego wpisu w tym samym składzie.
            var squad = await dbContext.GetTournamentPlayersByTournamentTeamIdAsync(tournamentTeam.Id, cancellationToken);
            var otherPlayerIds = squad.Where(tp => tp.Id != tournamentPlayer.Id).Select(tp => tp.PlayerId).ToList();
            if (otherPlayerIds.Count > 0 && !request.AllowDuplicate)
            {
                var others = await dbContext.GetPlayersByIdsAsync(otherPlayerIds, cancellationToken);
                var duplicate = others.Any(p =>
                    string.Equals(p.FirstName.Trim(), request.FirstName.Trim(), StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(p.LastName.Trim(), request.LastName.Trim(), StringComparison.OrdinalIgnoreCase) &&
                    p.BirthDate.Date == request.BirthDate.Date);

                if (duplicate)
                    return Result.Failure<UpdatePlayerInTournamentTeamResponse>(Error.Validation("LastName", "A player with the same name and birth date is already in this team"));
            }

            if (request.ShirtNumber.HasValue && request.ShirtNumber != tournamentPlayer.ShirtNumber)
            {
                var shirtNumberTaken = await dbContext.ShirtNumberExistsAsync(tournamentTeam.Id, request.ShirtNumber.Value, cancellationToken);
                if (shirtNumberTaken)
                    return Result.Failure<UpdatePlayerInTournamentTeamResponse>(Error.Validation("ShirtNumber", "Shirt number is already taken in this team"));
            }

            // DefaultPosition to pozycja "ogólna" profilu — edycja w składzie zmienia tylko pozycję w tym turnieju.
            var profileResult = player.UpdateDetails(request.FirstName, request.LastName, request.BirthDate, player.DefaultPosition, request.PhotoUrl);
            if (profileResult.IsFailure)
                return Result.Failure<UpdatePlayerInTournamentTeamResponse>(profileResult.Error);

            var squadResult = tournamentPlayer.UpdateDetails(request.ShirtNumber, request.Position, request.Status);
            if (squadResult.IsFailure)
                return Result.Failure<UpdatePlayerInTournamentTeamResponse>(squadResult.Error);

            foreach (var domainEvent in player.DomainEvents.Concat(tournamentPlayer.DomainEvents))
            {
                var outboxMessage = OutboxMessage.Create(
                    domainEvent.GetType().AssemblyQualifiedName!,
                    JsonSerializer.Serialize(domainEvent, domainEvent.GetType()));

                await dbContext.AddOutboxMessageAsync(outboxMessage, cancellationToken);
            }

            // Jeden SaveChanges = jedna transakcja: profil i wpis w składzie zmieniają się razem albo wcale.
            await dbContext.SaveChangesAsync(cancellationToken);

            return Result.Success(new UpdatePlayerInTournamentTeamResponse(player.Id, tournamentPlayer.Id));
        }
    }
}

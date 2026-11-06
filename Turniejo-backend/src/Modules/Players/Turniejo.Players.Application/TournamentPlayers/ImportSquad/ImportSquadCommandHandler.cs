using System.Text.Json;
using Turniejo.Players.Application.Abstractions;
using Turniejo.Players.Application.Services;
using Turniejo.Players.Domain.Entities;
using Turniejo.Players.Domain.Enums;
using Turniejo.SharedKernel.Contracts;
using Turniejo.SharedKernel.CQRS;
using Turniejo.SharedKernel.Outbox;
using Turniejo.SharedKernel.Results;

namespace Turniejo.Players.Application.TournamentPlayers.ImportSquad
{
    internal sealed class ImportSquadCommandHandler(
        IPlayersDbContext dbContext,
        ITeamModule teamModule,
        ITournamentModule tournamentModule,
        SquadEditGuard squadEditGuard,
        TeamRosterSync teamRosterSync,
        ICurrentUserContext currentUserContext) : ICommandHandler<ImportSquadCommand, ImportSquadResponse>
    {
        public async Task<Result<ImportSquadResponse>> Handle(ImportSquadCommand request, CancellationToken cancellationToken)
        {
            var tournamentTeam = await teamModule.GetTournamentTeamAsync(request.TournamentTeamId, cancellationToken);
            if (tournamentTeam == null || tournamentTeam.TournamentId != request.TournamentId)
                return Result.Failure<ImportSquadResponse>(Error.NotFound("TournamentTeam", request.TournamentTeamId));

            await squadEditGuard.EnsureCanEditSquadAsync(request.TournamentId, currentUserContext.UserId, cancellationToken);

            HashSet<Guid> sourcePlayerIds;
            if (request.SourceTournamentTeamId is not { } sourceTournamentTeamId)
            {
                // Kadra tej drużyny: tylko zawodnicy, którzy wciąż w niej są.
                sourcePlayerIds = (await dbContext.GetTeamPlayersByTeamIdAsync(tournamentTeam.TeamId, cancellationToken))
                    .Where(tp => tp.Status == TeamPlayerStatus.Active)
                    .Select(tp => tp.PlayerId)
                    .ToHashSet();
            }
            else
            {
                // Źródłem może być tylko inne zgłoszenie TEJ SAMEJ drużyny — inaczej trener jednej drużyny
                // mógłby wyciągać (i przepisywać do siebie) zawodników dowolnej innej, znając samo Id.
                var source = await teamModule.GetTournamentTeamAsync(sourceTournamentTeamId, cancellationToken);
                if (source == null || source.TeamId != tournamentTeam.TeamId || source.Id == tournamentTeam.Id)
                    return Result.Failure<ImportSquadResponse>(Error.NotFound("TournamentTeam", sourceTournamentTeamId));

                sourcePlayerIds = (await dbContext.GetTournamentPlayersByTournamentTeamIdAsync(source.Id, cancellationToken))
                    .Select(tp => tp.PlayerId)
                    .ToHashSet();
            }
            if (request.Players.Any(p => !sourcePlayerIds.Contains(p.PlayerId)))
                return Result.Failure<ImportSquadResponse>(Error.Validation("Players", "Player is not part of the source squad"));

            var profiles = await dbContext.GetPlayersByIdsAsync(request.Players.Select(p => p.PlayerId).ToList(), cancellationToken);
            if (profiles.Count != request.Players.Count)
                return Result.Failure<ImportSquadResponse>(Error.Validation("Players", "Player is not part of the source squad"));

            var category = await tournamentModule.GetTournamentCategoryAsync(tournamentTeam.CategoryId, cancellationToken);
            foreach (var profile in profiles)
            {
                var violation = CategoryAgeRule.Check(category, profile.BirthDate);
                if (violation != null)
                    return Result.Failure<ImportSquadResponse>(Error.Validation("Players", CategoryAgeRule.Message(violation)));
            }

            var currentSquad = await dbContext.GetTournamentPlayersByTournamentTeamIdAsync(tournamentTeam.Id, cancellationToken);

            var currentPlayerIds = currentSquad.Select(tp => tp.PlayerId).ToHashSet();
            if (request.Players.Any(p => currentPlayerIds.Contains(p.PlayerId)))
                return Result.Failure<ImportSquadResponse>(Error.Validation("Players", "Player is already assigned to this team"));

            var takenNumbers = currentSquad.Where(tp => tp.ShirtNumber.HasValue).Select(tp => tp.ShirtNumber!.Value).ToHashSet();
            foreach (var player in request.Players.Where(p => p.ShirtNumber.HasValue))
            {
                // Add zwraca false także dla powtórki w samym żądaniu.
                if (!takenNumbers.Add(player.ShirtNumber!.Value))
                    return Result.Failure<ImportSquadResponse>(Error.Validation("Players", "Shirt number is already taken in this team"));
            }

            var created = new List<TournamentPlayer>(request.Players.Count);
            foreach (var player in request.Players)
            {
                var result = TournamentPlayer.Create(
                    Guid.NewGuid(),
                    tournamentTeam.Id,
                    player.PlayerId,
                    player.ShirtNumber,
                    player.Position,
                    currentUserContext.UserId);

                if (result.IsFailure)
                    return Result.Failure<ImportSquadResponse>(result.Error);

                created.Add(result.Value);
            }

            foreach (var tournamentPlayer in created)
            {
                await dbContext.AddTournamentPlayerAsync(tournamentPlayer, cancellationToken);

                foreach (var domainEvent in tournamentPlayer.DomainEvents)
                {
                    var outboxMessage = OutboxMessage.Create(
                        domainEvent.GetType().AssemblyQualifiedName!,
                        JsonSerializer.Serialize(domainEvent, domainEvent.GetType()));

                    await dbContext.AddOutboxMessageAsync(outboxMessage, cancellationToken);
                }
            }

            var rosterResult = await teamRosterSync.EnsureInRosterAsync(
                tournamentTeam.TeamId, request.Players.Select(p => (p.PlayerId, p.ShirtNumber)), cancellationToken);
            if (rosterResult.IsFailure)
                return Result.Failure<ImportSquadResponse>(rosterResult.Error);

            await dbContext.SaveChangesAsync(cancellationToken);

            return Result.Success(new ImportSquadResponse(created.Select(tp => tp.Id).ToList()));
        }
    }
}

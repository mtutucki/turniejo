using Turniejo.Players.Application.Abstractions;
using Turniejo.Players.Application.Services;
using Turniejo.SharedKernel.Contracts;
using Turniejo.SharedKernel.CQRS;
using Turniejo.SharedKernel.Exceptions;
using Turniejo.SharedKernel.Results;

namespace Turniejo.Players.Application.TeamPlayers.RemoveTeamPlayer
{
    internal sealed class RemoveTeamPlayerCommandHandler(
        IPlayersDbContext dbContext,
        TeamRosterAccessGuard accessGuard,
        TeamSquadsReader squadsReader,
        SquadRemoval squadRemoval,
        ICurrentUserContext currentUserContext) : ICommandHandler<RemoveTeamPlayerCommand, RemoveTeamPlayerResponse>
    {
        public async Task<Result<RemoveTeamPlayerResponse>> Handle(RemoveTeamPlayerCommand request, CancellationToken cancellationToken)
        {
            var access = await accessGuard.GetAccessAsync(request.TeamId, currentUserContext.UserId, cancellationToken);
            if (access != TeamRosterAccess.Edit)
                throw new UnauthorizedException();

            var member = await dbContext.GetTeamPlayerAsync(request.TeamId, request.PlayerId, cancellationToken);
            if (member == null)
                return Result.Failure<RemoveTeamPlayerResponse>(Error.NotFound("Player", request.PlayerId));

            var removedSquadEntryIds = new HashSet<Guid>();
            var kept = new List<string>();
            var removedFromSquads = 0;

            if (request.RemoveFromCurrentSquads)
            {
                var current = (await squadsReader.ReadAsync(request.TeamId, currentUserContext.UserId, cancellationToken))
                    .Where(e => e.IsCurrent && e.TournamentPlayer.PlayerId == request.PlayerId)
                    .ToList();

                foreach (var entry in current)
                {
                    if (!entry.Squad.CanEditSquad)
                    {
                        kept.Add(entry.Squad.TournamentName);
                        continue;
                    }

                    // Czytnik zwraca wpisy bez śledzenia, a usunięcie i zmiana statusu muszą trafić do bazy.
                    var tracked = await dbContext.GetTournamentPlayerByIdAsync(entry.TournamentPlayer.Id, cancellationToken);
                    if (tracked == null)
                        continue;

                    var removal = await squadRemoval.RemoveAsync(tracked, cancellationToken);
                    if (removal.IsFailure)
                        return Result.Failure<RemoveTeamPlayerResponse>(removal.Error);

                    if (removal.Value)
                        removedSquadEntryIds.Add(tracked.Id);
                    removedFromSquads++;
                }
            }

            var deleted = await CanDeleteProfileAsync(request.TeamId, request.PlayerId, removedSquadEntryIds, cancellationToken);
            if (deleted)
            {
                var player = await dbContext.GetPlayerByIdAsync(request.PlayerId, cancellationToken);
                await dbContext.RemoveTeamPlayerAsync(member, cancellationToken);
                if (player != null)
                    await dbContext.RemovePlayerAsync(player, cancellationToken);
            }
            else
            {
                member.Leave();
            }

            await dbContext.SaveChangesAsync(cancellationToken);
            return Result.Success(new RemoveTeamPlayerResponse(request.PlayerId, deleted, removedFromSquads, kept.Distinct().ToList()));
        }

        // Profil znika tylko, gdy nie ma po nim żadnego śladu: żadnego składu (poza usuniętymi właśnie bez
        // goli i kartek), innej kadry, konta ani opiekunów.
        private async Task<bool> CanDeleteProfileAsync(
            Guid teamId, Guid playerId, HashSet<Guid> removedSquadEntryIds, CancellationToken cancellationToken)
        {
            var player = await dbContext.GetPlayerByIdAsync(playerId, cancellationToken);
            if (player == null || player.UserId != null)
                return false;

            var squads = await dbContext.GetTournamentPlayersByPlayerIdAsync(playerId, cancellationToken);
            if (squads.Any(tp => !removedSquadEntryIds.Contains(tp.Id)))
                return false;

            var rosters = await dbContext.GetTeamPlayersByPlayerIdAsync(playerId, cancellationToken);
            if (rosters.Any(r => r.TeamId != teamId))
                return false;

            var guardians = await dbContext.GetPlayerGuardiansByPlayerIdAsync(playerId, cancellationToken);
            return guardians.Count == 0;
        }
    }
}

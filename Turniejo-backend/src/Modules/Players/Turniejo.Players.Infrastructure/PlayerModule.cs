using Turniejo.Players.Application.Abstractions;
using Turniejo.SharedKernel.Contracts;

namespace Turniejo.Players.Infrastructure;

internal sealed class PlayerModule(IPlayersDbContext dbContext) : IPlayerModule
{
    public async Task<IReadOnlyList<TournamentTeamPlayerDto>> GetTournamentTeamPlayersAsync(Guid tournamentTeamId, CancellationToken cancellationToken = default)
    {
        var tournamentPlayers = await dbContext.GetTournamentPlayersByTournamentTeamIdAsync(tournamentTeamId, cancellationToken);
        if (tournamentPlayers.Count == 0)
            return [];

        var playerIds = tournamentPlayers.Select(tp => tp.PlayerId).ToList();
        var players = await dbContext.GetPlayersByIdsAsync(playerIds, cancellationToken);
        var playersById = players.ToDictionary(p => p.Id);

        return tournamentPlayers
            .Where(tp => playersById.ContainsKey(tp.PlayerId))
            .Select(tp =>
            {
                var player = playersById[tp.PlayerId];
                return new TournamentTeamPlayerDto(
                    tp.Id, tp.TournamentTeamId, tp.PlayerId,
                    player.FirstName, player.LastName,
                    tp.ShirtNumber, tp.Position?.ToString(), tp.Status.ToString());
            })
            .ToList();
    }

    public async Task<IReadOnlyList<PlayerDto>> GetPlayersAsync(IReadOnlyCollection<Guid> playerIds, CancellationToken cancellationToken = default)
    {
        if (playerIds.Count == 0)
            return [];

        var players = await dbContext.GetPlayersByIdsAsync(playerIds, cancellationToken);

        return players.Select(p => new PlayerDto(p.Id, p.FirstName, p.LastName)).ToList();
    }

    public async Task<bool> IsGuardianOfAnyPlayerOnTeamsAsync(
        Guid userId, IReadOnlyCollection<Guid> tournamentTeamIds, CancellationToken cancellationToken = default)
    {
        if (tournamentTeamIds.Count == 0)
            return false;

        var guardianships = await dbContext.GetGuardianshipsByUserIdAsync(userId, cancellationToken);
        if (guardianships.Count == 0)
            return false;

        var guardedPlayerIds = guardianships.Select(g => g.PlayerId).ToHashSet();

        foreach (var tournamentTeamId in tournamentTeamIds)
        {
            var roster = await dbContext.GetTournamentPlayersByTournamentTeamIdAsync(tournamentTeamId, cancellationToken);
            if (roster.Any(tp => guardedPlayerIds.Contains(tp.PlayerId)))
                return true;
        }

        return false;
    }

    public async Task<IReadOnlyList<Guid>> GetNotifiableGuardianUserIdsForPlayersAsync(
        IReadOnlyCollection<Guid> playerIds, CancellationToken cancellationToken = default)
    {
        if (playerIds.Count == 0)
            return [];

        var guardianships = await dbContext.GetPlayerGuardiansByPlayerIdsAsync(playerIds, cancellationToken);

        return guardianships
            .Where(g => g.CanReceiveNotifications)
            .Select(g => g.UserId)
            .Distinct()
            .ToList();
    }
}

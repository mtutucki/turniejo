using Turniejo.Players.Application.Abstractions;
using Turniejo.Players.Application.DTOs;
using Turniejo.SharedKernel.Contracts;

namespace Turniejo.Players.Application.Services
{
    public sealed class MyChildMatchResolver(
        IPlayersDbContext dbContext,
        IMatchModule matchModule,
        ITeamModule teamModule,
        ITournamentModule tournamentModule)
    {
        public async Task<IReadOnlyList<MyChildMatchDto>> ResolveAsync(
            Guid playerId, Func<TournamentMatchDto, bool> filter, CancellationToken cancellationToken = default)
        {
            var tournamentPlayers = await dbContext.GetTournamentPlayersByPlayerIdAsync(playerId, cancellationToken);
            var tournamentTeamIds = tournamentPlayers.Select(p => p.TournamentTeamId).Distinct().ToList();
            if (tournamentTeamIds.Count == 0)
                return [];

            var matches = await matchModule.GetMatchesByTournamentTeamIdsAsync(tournamentTeamIds, cancellationToken);

            var teamNameCache = new Dictionary<Guid, string>();
            var tournamentNameCache = new Dictionary<Guid, string>();

            var dtos = new List<MyChildMatchDto>();
            foreach (var match in matches.Where(filter).OrderBy(m => m.ScheduledAt))
            {
                var homeTeamName = await GetTeamNameAsync(match.HomeTournamentTeamId, teamNameCache, cancellationToken);
                var awayTeamName = await GetTeamNameAsync(match.AwayTournamentTeamId, teamNameCache, cancellationToken);
                var tournamentName = await GetTournamentNameAsync(match.TournamentId, tournamentNameCache, cancellationToken);

                dtos.Add(new MyChildMatchDto(
                    match.Id, match.TournamentId, tournamentName, match.ScheduledAt, match.Status,
                    homeTeamName, awayTeamName, match.HomeScore, match.AwayScore));
            }

            return dtos;
        }

        private async Task<string> GetTeamNameAsync(Guid tournamentTeamId, Dictionary<Guid, string> cache, CancellationToken cancellationToken)
        {
            if (cache.TryGetValue(tournamentTeamId, out var cached))
                return cached;

            var tournamentTeam = await teamModule.GetTournamentTeamAsync(tournamentTeamId, cancellationToken);
            var team = tournamentTeam != null ? await teamModule.GetTeamAsync(tournamentTeam.TeamId, cancellationToken) : null;
            var name = team?.Name ?? "Unknown";
            cache[tournamentTeamId] = name;
            return name;
        }

        private async Task<string> GetTournamentNameAsync(Guid tournamentId, Dictionary<Guid, string> cache, CancellationToken cancellationToken)
        {
            if (cache.TryGetValue(tournamentId, out var cached))
                return cached;

            var tournament = await tournamentModule.GetTournamentAsync(tournamentId, cancellationToken);
            var name = tournament?.Name ?? "Unknown";
            cache[tournamentId] = name;
            return name;
        }
    }
}

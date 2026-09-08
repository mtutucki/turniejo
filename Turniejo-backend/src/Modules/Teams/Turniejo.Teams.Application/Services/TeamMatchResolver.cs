using Turniejo.SharedKernel.Contracts;
using Turniejo.Teams.Application.Abstractions;
using Turniejo.Teams.Application.DTOs;

namespace Turniejo.Teams.Application.Services
{
    public sealed class TeamMatchResolver(ITeamsDbContext dbContext, IMatchModule matchModule)
    {
        public async Task<IReadOnlyList<TeamMatchDto>> ResolveAsync(
            Guid tournamentTeamId, Func<TournamentMatchDto, bool> filter, CancellationToken cancellationToken = default)
        {
            var matches = await matchModule.GetMatchesByTournamentTeamIdsAsync([tournamentTeamId], cancellationToken);

            var teamNameCache = new Dictionary<Guid, string>();

            var dtos = new List<TeamMatchDto>();
            foreach (var match in matches.Where(filter).OrderBy(m => m.ScheduledAt))
            {
                var homeTeamName = await GetTeamNameAsync(match.HomeTournamentTeamId, teamNameCache, cancellationToken);
                var awayTeamName = await GetTeamNameAsync(match.AwayTournamentTeamId, teamNameCache, cancellationToken);

                dtos.Add(new TeamMatchDto(
                    match.Id, match.ScheduledAt, match.Status, homeTeamName, awayTeamName, match.HomeScore, match.AwayScore));
            }

            return dtos;
        }

        private async Task<string> GetTeamNameAsync(Guid tournamentTeamId, Dictionary<Guid, string> cache, CancellationToken cancellationToken)
        {
            if (cache.TryGetValue(tournamentTeamId, out var cached))
                return cached;

            var tournamentTeam = await dbContext.GetTournamentTeamByIdAsync(tournamentTeamId, cancellationToken);
            var team = tournamentTeam != null ? await dbContext.GetTeamByIdAsync(tournamentTeam.TeamId, cancellationToken) : null;
            var name = team?.Name ?? "Unknown";
            cache[tournamentTeamId] = name;
            return name;
        }
    }
}

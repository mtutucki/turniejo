using Turniejo.Players.Application.Abstractions;
using Turniejo.Players.Application.DTOs;
using Turniejo.Players.Application.Services;
using Turniejo.SharedKernel.Contracts;
using Turniejo.SharedKernel.CQRS;
using Turniejo.SharedKernel.Results;

namespace Turniejo.Players.Application.PlayerGuardians.GetMyChildStanding
{
    internal sealed class GetMyChildStandingQueryHandler(
        IPlayersDbContext dbContext,
        GuardianAccessChecker accessChecker,
        ITeamModule teamModule,
        IStandingModule standingModule,
        ITournamentModule tournamentModule,
        ICurrentUserContext currentUserContext) : IQueryHandler<GetMyChildStandingQuery, IReadOnlyList<MyChildStandingDto>>
    {
        public async Task<Result<IReadOnlyList<MyChildStandingDto>>> Handle(GetMyChildStandingQuery request, CancellationToken cancellationToken)
        {
            await accessChecker.EnsureGuardianAsync(request.PlayerId, currentUserContext.UserId, cancellationToken);

            var tournamentPlayers = await dbContext.GetTournamentPlayersByPlayerIdAsync(request.PlayerId, cancellationToken);
            var tournamentTeamIds = tournamentPlayers.Select(p => p.TournamentTeamId).Distinct().ToList();

            var dtos = new List<MyChildStandingDto>();
            foreach (var tournamentTeamId in tournamentTeamIds)
            {
                var tournamentTeam = await teamModule.GetTournamentTeamAsync(tournamentTeamId, cancellationToken);
                if (tournamentTeam?.GroupId == null)
                    continue;

                // Turniej pucharowy nie ma fazy grupowej — drużyny siedzą w technicznej grupie tylko po to,
                // żeby dało się rozstawić drabinkę. Tabela z samymi zerami nic rodzicowi nie mówi.
                var tournament = await tournamentModule.GetTournamentAsync(tournamentTeam.TournamentId, cancellationToken);
                if (tournament?.Format == "Knockout")
                    continue;

                var groupId = tournamentTeam.GroupId.Value;
                var computed = await standingModule.GetGroupStandingsAsync(groupId, cancellationToken);
                var group = await tournamentModule.GetTournamentGroupAsync(groupId, cancellationToken);

                // Tabela liczy tylko rozegrane mecze — przed pierwszym gwizdkiem byłaby pusta. Rodzic ma
                // widzieć cały skład grupy, więc brakujące drużyny dopisujemy z zerami na końcu.
                // Tylko zaakceptowane: wycofana/odrzucona drużyna zachowuje GroupId, ale w grupie już nie gra.
                var groupTeamIds = (await teamModule.GetTournamentTeamsByTournamentIdAsync(tournamentTeam.TournamentId, cancellationToken))
                    .Where(t => t.GroupId == groupId && t.RegistrationStatus == "Accepted")
                    .Select(t => t.Id);
                var standings = computed
                    .Concat(groupTeamIds
                        .Where(id => computed.All(r => r.TournamentTeamId != id))
                        .Select((id, i) => new GroupStandingDto(id, computed.Count + i + 1, 0)))
                    .ToList();

                var teamNames = new Dictionary<Guid, string>();
                foreach (var row in standings)
                {
                    var rowTeam = await teamModule.GetTournamentTeamAsync(row.TournamentTeamId, cancellationToken);
                    var team = rowTeam != null ? await teamModule.GetTeamAsync(rowTeam.TeamId, cancellationToken) : null;
                    teamNames[row.TournamentTeamId] = team?.Name ?? "Drużyna";
                }

                dtos.Add(new MyChildStandingDto(tournamentTeamId, tournamentTeam.GroupId.Value, standings, group?.Name, teamNames, tournament?.Name));
            }

            return Result.Success<IReadOnlyList<MyChildStandingDto>>(dtos);
        }
    }
}

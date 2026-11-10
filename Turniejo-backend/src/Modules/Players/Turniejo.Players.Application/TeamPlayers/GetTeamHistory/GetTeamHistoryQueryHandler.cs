using Turniejo.Players.Application.Abstractions;
using Turniejo.Players.Application.DTOs;
using Turniejo.Players.Application.Services;
using Turniejo.SharedKernel.Contracts;
using Turniejo.SharedKernel.CQRS;
using Turniejo.SharedKernel.Exceptions;
using Turniejo.SharedKernel.Results;
using static Turniejo.Players.Application.TeamPlayers.GetTeamHistory.TeamHistoryCalculator;

namespace Turniejo.Players.Application.TeamPlayers.GetTeamHistory
{
    /// <summary>
    /// Zbiera zgłoszenia drużyny z bazy (moduł Teams), turnieje, kategorie i grupy (Tournaments), mecze
    /// i zdarzenia (Matches), tabele (Standings) i drabinki (Brackets), a liczy TeamHistoryCalculator.
    /// Gole i asysty jak w rankingach turnieju: gol i gol z karnego, asysta to drugi zawodnik przy golu.
    /// </summary>
    internal sealed class GetTeamHistoryQueryHandler(
        IPlayersDbContext dbContext,
        ITeamModule teamModule,
        ITournamentModule tournamentModule,
        IMatchModule matchModule,
        IStandingModule standingModule,
        IBracketModule bracketModule,
        TeamRosterAccessGuard accessGuard,
        ICurrentUserContext currentUserContext) : IQueryHandler<GetTeamHistoryQuery, TeamHistoryDto>
    {
        private static readonly HashSet<string> GoalTypes = ["Goal", "PenaltyGoal"];

        public async Task<Result<TeamHistoryDto>> Handle(GetTeamHistoryQuery request, CancellationToken cancellationToken)
        {
            var team = await teamModule.GetTeamAsync(request.TeamId, cancellationToken);
            if (team == null)
                return Result.Failure<TeamHistoryDto>(Error.NotFound("Team", request.TeamId));
            if (await accessGuard.GetAccessAsync(request.TeamId, currentUserContext.UserId, cancellationToken) == TeamRosterAccess.None)
                throw new UnauthorizedException();

            var registrations = (await teamModule.GetTournamentTeamsByTeamIdAsync(request.TeamId, cancellationToken))
                .Where(r => r.RegistrationStatus == "Accepted")
                .ToList();

            var tournaments = new Dictionary<Guid, TournamentDto>();
            foreach (var tournamentId in registrations.Select(r => r.TournamentId).Distinct())
            {
                var tournament = await tournamentModule.GetTournamentAsync(tournamentId, cancellationToken);
                if (tournament != null && tournament.Status != "Cancelled")
                    tournaments[tournamentId] = tournament;
            }
            registrations = registrations.Where(r => tournaments.ContainsKey(r.TournamentId)).ToList();
            var ownIds = registrations.Select(r => r.Id).ToHashSet();

            var matches = (await matchModule.GetMatchesByTournamentTeamIdsAsync(ownIds, cancellationToken))
                .Where(m => m.Status != "Cancelled")
                .ToList();
            var matchesById = matches.ToDictionary(m => m.Id);
            var finished = matches.Where(m => m.Status == "Finished").OrderBy(m => m.ScheduledAt).ToList();
            Guid Own(TournamentMatchDto m) => ownIds.Contains(m.HomeTournamentTeamId) ? m.HomeTournamentTeamId : m.AwayTournamentTeamId;
            Guid Opponent(TournamentMatchDto m) => ownIds.Contains(m.HomeTournamentTeamId) ? m.AwayTournamentTeamId : m.HomeTournamentTeamId;

            // Zgłoszenia przeciwników → drużyny z bazy (bilans z rywalem łączy turnieje i kategorie).
            var clubOf = new Dictionary<Guid, Guid>();
            foreach (var tournamentId in tournaments.Keys)
                foreach (var entry in await teamModule.GetTournamentTeamsByTournamentIdAsync(tournamentId, cancellationToken))
                    clubOf[entry.Id] = entry.TeamId;

            var history = new List<TeamHistoryTournamentDto>();
            foreach (var group in registrations.GroupBy(r => r.TournamentId))
            {
                var tournament = tournaments[group.Key];
                var groups = await tournamentModule.GetTournamentGroupsAsync(tournament.Id, cancellationToken);
                var nodes = tournament.Format is "Knockout" or "GroupAndKnockout"
                    ? await bracketModule.GetNodesByTournamentIdAsync(tournament.Id, cancellationToken)
                    : [];
                // Dwie fazy grupowe: wynik turnieju to miejsce w grupie drugiej fazy, o ile drużyna do niej trafiła.
                var secondStageSeats = tournament.Format == "TwoGroupStages"
                    ? await bracketModule.GetSecondStageSeatsAsync(tournament.Id, cancellationToken)
                    : [];
                foreach (var registration in group)
                {
                    var category = await tournamentModule.GetTournamentCategoryAsync(registration.CategoryId, cancellationToken);
                    var record = Record(finished.Where(m => Own(m) == registration.Id).Select(m => (m, registration.Id)));
                    var cup = Cup(nodes.Where(n => n.CategoryId == registration.CategoryId).ToList(), matchesById, registration.Id);

                    int? position = null;
                    string? groupName = null;
                    // Najpierw grupa drugiej fazy; dopóki drużyna nie zagrała w niej meczu, liczy się grupa fazy 1.
                    var groupIds = new[] { secondStageSeats.FirstOrDefault(x => x.TournamentTeamId == registration.Id)?.GroupId, registration.GroupId };
                    foreach (var groupId in cup == null ? groupIds.OfType<Guid>() : [])
                    {
                        var row = (await standingModule.GetGroupStandingsAsync(groupId, cancellationToken))
                            .FirstOrDefault(s => s.TournamentTeamId == registration.Id);
                        if (row is { MatchesPlayed: > 0 })
                        {
                            position = row.Position;
                            groupName = groups.FirstOrDefault(g => g.Id == groupId)?.Name;
                            break;
                        }
                    }

                    history.Add(new TeamHistoryTournamentDto(
                        tournament.Id, tournament.Name, tournament.Status, tournament.StartAt, tournament.EndAt,
                        registration.Id, category?.Name,
                        cup?.Kind ?? (position != null ? "Group" : "None"), cup?.EliminatedRoundFromEnd,
                        position, groupName, record));
                }
            }

            var players = await PlayersAsync(registrations.Select(r => r.Id).ToList(), finished, cancellationToken);

            var opponentNames = (await teamModule.GetTeamsAsync(
                    finished.Select(m => clubOf.GetValueOrDefault(Opponent(m))).Where(id => id != Guid.Empty).Distinct().ToList(),
                    cancellationToken))
                .ToDictionary(t => t.Id, t => t.Name);
            var opponents = finished
                .Where(m => clubOf.ContainsKey(Opponent(m)))
                .GroupBy(m => clubOf[Opponent(m)])
                .Select(g => new TeamHistoryOpponentDto(
                    g.Key, opponentNames.GetValueOrDefault(g.Key, "Drużyna"), Record(g.Select(m => (m, Own(m))))))
                .OrderByDescending(o => o.Record.Played)
                .ThenBy(o => o.Name)
                .ToList();

            var form = finished.Select(m => Outcome(m, Own(m))!).TakeLast(5).ToList();

            return Result.Success(new TeamHistoryDto(
                Record(finished.Select(m => (m, Own(m)))),
                tournaments.Count,
                form,
                history.OrderByDescending(h => h.StartAt).ThenBy(h => h.CategoryName).ToList(),
                players,
                opponents));
        }

        private async Task<IReadOnlyList<TeamHistoryPlayerDto>> PlayersAsync(
            IReadOnlyList<Guid> tournamentTeamIds, List<TournamentMatchDto> finished, CancellationToken cancellationToken)
        {
            var finishedIds = finished.Select(m => m.Id).ToHashSet();
            var events = new List<TeamMatchEventDto>();
            foreach (var id in tournamentTeamIds)
                events.AddRange((await matchModule.GetMatchEventsByTournamentTeamIdAsync(id, cancellationToken))
                    .Where(e => finishedIds.Contains(e.MatchId)));

            var stats = new Dictionary<Guid, (int Goals, int Assists, int Mvp, int Yellow, int Red)>();
            void Add(Guid? playerId, Func<(int Goals, int Assists, int Mvp, int Yellow, int Red), (int, int, int, int, int)> update)
            {
                if (playerId is not { } id)
                    return;
                stats[id] = update(stats.GetValueOrDefault(id));
            }

            foreach (var e in events)
            {
                if (GoalTypes.Contains(e.Type))
                {
                    Add(e.PlayerId, s => (s.Goals + 1, s.Assists, s.Mvp, s.Yellow, s.Red));
                    Add(e.RelatedPlayerId, s => (s.Goals, s.Assists + 1, s.Mvp, s.Yellow, s.Red));
                }
                else if (e.Type == "MvpSelected")
                    Add(e.PlayerId, s => (s.Goals, s.Assists, s.Mvp + 1, s.Yellow, s.Red));
                else if (e.Type == "YellowCard")
                    Add(e.PlayerId, s => (s.Goals, s.Assists, s.Mvp, s.Yellow + 1, s.Red));
                else if (e.Type == "RedCard")
                    Add(e.PlayerId, s => (s.Goals, s.Assists, s.Mvp, s.Yellow, s.Red + 1));
            }

            var profiles = (await dbContext.GetPlayersByIdsAsync(stats.Keys.ToList(), cancellationToken)).ToDictionary(p => p.Id);
            return stats
                .Where(s => profiles.ContainsKey(s.Key))
                .Select(s => new TeamHistoryPlayerDto(
                    s.Key, profiles[s.Key].FirstName, profiles[s.Key].LastName,
                    s.Value.Goals, s.Value.Assists, s.Value.Mvp, s.Value.Yellow, s.Value.Red))
                .OrderByDescending(p => p.Goals)
                .ThenByDescending(p => p.Assists)
                .ThenByDescending(p => p.Mvp)
                .ThenBy(p => p.LastName)
                .ToList();
        }
    }
}

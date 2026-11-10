using Turniejo.SharedKernel.Contracts;
using Turniejo.SharedKernel.Results;
using Turniejo.Standings.Application.DTOs;
using Turniejo.Standings.Application.Standings.RecalculateStandings;
using Turniejo.Standings.Domain.Entities;

namespace Turniejo.Standings.Application.Standings
{
    /// <summary>
    /// Tabela grupy policzona z zakończonych meczów: zaakceptowane drużyny grupy z miejscami wg zasad turnieju.
    /// Zapisuje ją przeliczenie po meczu, a odczyt liczy ją sam, dopóki zapisu brakuje.
    /// </summary>
    internal sealed record GroupTable(TournamentGroupDto Group, IReadOnlyList<GroupTable.Row> Rows)
    {
        public sealed record Row(StandingCalculator.TeamStats Stats, int Position);

        public static async Task<Result<GroupTable>> ComputeAsync(
            Guid groupId, ITournamentModule tournamentModule, ITeamModule teamModule, IMatchModule matchModule,
            IBracketModule bracketModule, CancellationToken cancellationToken)
        {
            var group = await tournamentModule.GetTournamentGroupAsync(groupId, cancellationToken);
            if (group == null)
                return Result.Failure<GroupTable>(Error.NotFound("TournamentGroup", groupId));

            var settings = await tournamentModule.GetTournamentSettingsAsync(group.TournamentId, cancellationToken);
            if (settings == null)
                return Result.Failure<GroupTable>(Error.NotFound("TournamentSettings", group.TournamentId));

            var tieBreakerRules = await tournamentModule.GetTieBreakerRulesAsync(group.TournamentId, cancellationToken);
            var groupTeamIds = await AcceptedTeamIdsAsync(teamModule, bracketModule, group, cancellationToken);
            var matches = await matchModule.GetFinishedMatchesByGroupIdAsync(groupId, cancellationToken);

            var stats = groupTeamIds
                .Select(id => StandingCalculator.ComputeStats(id, matches, settings.PointsForWin, settings.PointsForDraw, settings.PointsForLoss))
                .ToList();
            var sorted = StandingCalculator.SortTeams(
                stats, matches, tieBreakerRules, settings.PointsForWin, settings.PointsForDraw, settings.PointsForLoss).ToList();

            var rows = stats
                .Select(s => new Row(s, sorted.IndexOf(s.TournamentTeamId) + 1))
                .OrderBy(r => r.Position)
                .ToList();
            return Result.Success(new GroupTable(group, rows));
        }

        /// <summary>
        /// Zaakceptowane drużyny grupy. W grupie drugiej fazy (format „Dwie fazy grupowe”) to drużyny
        /// z obsadzonych miejsc - przypisanie do grupy (GroupId) wskazuje zawsze grupę fazy 1.
        /// </summary>
        public static async Task<IReadOnlyList<Guid>> AcceptedTeamIdsAsync(
            ITeamModule teamModule, IBracketModule bracketModule, TournamentGroupDto group, CancellationToken cancellationToken)
        {
            var accepted = (await teamModule.GetTournamentTeamsByTournamentIdAsync(group.TournamentId, cancellationToken))
                .Where(t => t.RegistrationStatus == "Accepted")
                .ToList();
            if (group.Stage != 2)
                return accepted.Where(t => t.GroupId == group.Id).Select(t => t.Id).ToList();

            var acceptedIds = accepted.Select(t => t.Id).ToHashSet();
            return (await bracketModule.GetSecondStageSeatsAsync(group.TournamentId, cancellationToken))
                .Where(s => s.GroupId == group.Id && s.TournamentTeamId is { } id && acceptedIds.Contains(id))
                .OrderBy(s => s.Order)
                .Select(s => s.TournamentTeamId!.Value)
                .ToList();
        }

        /// <summary>
        /// Tabela do pokazania: zapisane wiersze, a gdy którejś drużynie grupy wiersza brakuje (żaden mecz grupy
        /// się jeszcze nie skończył albo drużyna doszła po przeliczeniu) - tabela policzona na bieżąco, bez zapisu.
        /// </summary>
        public static async Task<IReadOnlyList<StandingDto>> ReadAsync(
            Guid tournamentId, Guid groupId, IReadOnlyList<Standing> saved,
            ITournamentModule tournamentModule, ITeamModule teamModule, IMatchModule matchModule, IBracketModule bracketModule,
            CancellationToken cancellationToken)
        {
            var rows = saved.Where(s => s.TournamentId == tournamentId).ToList();
            var groupInfo = await tournamentModule.GetTournamentGroupAsync(groupId, cancellationToken);
            if (groupInfo == null || groupInfo.TournamentId != tournamentId)
                return rows.OrderBy(s => s.Position).Select(ToDto).ToList();

            var groupTeamIds = await AcceptedTeamIdsAsync(teamModule, bracketModule, groupInfo, cancellationToken);
            if (groupTeamIds.All(id => rows.Any(r => r.TournamentTeamId == id)))
                return rows.OrderBy(s => s.Position).Select(ToDto).ToList();

            var table = await ComputeAsync(groupId, tournamentModule, teamModule, matchModule, bracketModule, cancellationToken);
            // Grupa z innego turnieju niż ten z autoryzacji: nic nie zdradzamy.
            if (table.IsFailure || table.Value.Group.TournamentId != tournamentId)
                return rows.OrderBy(s => s.Position).Select(ToDto).ToList();

            var group = table.Value.Group;
            return table.Value.Rows
                .Select(r => new StandingDto(
                    rows.FirstOrDefault(s => s.TournamentTeamId == r.Stats.TournamentTeamId)?.Id ?? Guid.Empty,
                    tournamentId, group.CategoryId, groupId, r.Stats.TournamentTeamId,
                    r.Stats.MatchesPlayed, r.Stats.Wins, r.Stats.Draws, r.Stats.Losses,
                    r.Stats.GoalsFor, r.Stats.GoalsAgainst, r.Stats.GoalsFor - r.Stats.GoalsAgainst,
                    r.Stats.Points, r.Position))
                .ToList();
        }

        private static StandingDto ToDto(Standing s) => new(
            s.Id, s.TournamentId, s.CategoryId, s.GroupId, s.TournamentTeamId,
            s.MatchesPlayed, s.Wins, s.Draws, s.Losses, s.GoalsFor, s.GoalsAgainst,
            s.GoalDifference, s.Points, s.Position);
    }
}

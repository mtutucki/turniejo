using Turniejo.SharedKernel.Contracts;
using Turniejo.SharedKernel.CQRS;
using Turniejo.SharedKernel.Results;
using Turniejo.Tournaments.Application.Abstractions;
using Turniejo.Tournaments.Application.DTOs;

namespace Turniejo.Tournaments.Application.Tournaments.GetTournamentDashboard
{
    internal sealed class GetTournamentDashboardQueryHandler(
        ITournamentsDbContext tournamentsDbContext,
        ITeamModule teamModule,
        IMatchModule matchModule) : IQueryHandler<GetTournamentDashboardQuery, TournamentDashboardDto>
    {
        public async Task<Result<TournamentDashboardDto>> Handle(GetTournamentDashboardQuery request, CancellationToken cancellationToken)
        {
            var tournament = await tournamentsDbContext.GetTournamentByIdAsync(request.TournamentId, cancellationToken);
            if (tournament == null)
                return Result.Failure<TournamentDashboardDto>(Error.NotFound("Tournament", request.TournamentId));

            var tournamentTeams = await teamModule.GetTournamentTeamsByTournamentIdAsync(request.TournamentId, cancellationToken);
            var teamCounts = new TeamCountsDto(
                Pending: tournamentTeams.Count(t => t.RegistrationStatus == "Pending"),
                Accepted: tournamentTeams.Count(t => t.RegistrationStatus == "Accepted"),
                Rejected: tournamentTeams.Count(t => t.RegistrationStatus == "Rejected"),
                Withdrawn: tournamentTeams.Count(t => t.RegistrationStatus == "Withdrawn"),
                Total: tournamentTeams.Count);

            var matches = await matchModule.GetMatchesByTournamentIdAsync(request.TournamentId, cancellationToken);
            var finishedCount = matches.Count(m => m.Status == "Finished");
            var scheduledCount = matches.Count(m => m.Status != "Finished" && m.Status != "Cancelled");
            var matchCounts = new MatchCountsDto(finishedCount, scheduledCount, matches.Count);

            var scheduleStatus = GetScheduleStatus(matches.Count, finishedCount);

            // Bez ScheduledAt >= now zaległy mecz utknięty w Scheduled/Ready (np. organizator
            // spóźniony, sędzia nigdy nie wystartował meczu) wypływał na górę listy przed
            // realnie przyszłymi meczami, bo sortowanie samo w sobie tego nie odróżnia.
            var now = DateTimeOffset.UtcNow;
            var upcomingCount = Math.Clamp(request.UpcomingMatchesCount, 1, 50);
            var upcomingMatches = matches
                .Where(m => m.Status is "Scheduled" or "Ready" && m.ScheduledAt >= now)
                .OrderBy(m => m.ScheduledAt)
                .Take(upcomingCount)
                .ToList();

            // Nazwy drużyn rozwiązane z już pobranej listy tournamentTeams + JEDNO zbiorcze zapytanie
            // o Teamy, zamiast 4 sekwencyjnych zapytań per mecz (GetTournamentTeamAsync+GetTeamAsync
            // x2) — przy nieograniczonym wcześniej UpcomingMatchesCount to było realne ryzyko DoS.
            var tournamentTeamsById = tournamentTeams.ToDictionary(t => t.Id);
            var teamIds = upcomingMatches
                .SelectMany(m => new[] { m.HomeTournamentTeamId, m.AwayTournamentTeamId })
                .Where(tournamentTeamsById.ContainsKey)
                .Select(id => tournamentTeamsById[id].TeamId)
                .Distinct()
                .ToList();
            var teamsById = (await teamModule.GetTeamsAsync(teamIds, cancellationToken)).ToDictionary(t => t.Id);

            var upcomingMatchDtos = upcomingMatches
                .Select(match => new UpcomingMatchDto(
                    match.Id, match.ScheduledAt, match.Status,
                    GetTeamName(match.HomeTournamentTeamId, tournamentTeamsById, teamsById),
                    GetTeamName(match.AwayTournamentTeamId, tournamentTeamsById, teamsById)))
                .ToList();

            var dashboard = new TournamentDashboardDto(
                tournament.Id,
                tournament.Name,
                tournament.Status.ToString(),
                teamCounts,
                matchCounts,
                scheduleStatus,
                upcomingMatchDtos);

            return Result.Success(dashboard);
        }

        private static string GetScheduleStatus(int totalMatches, int finishedCount)
        {
            if (totalMatches == 0)
                return "NotGenerated";

            if (finishedCount == totalMatches)
                return "Completed";

            if (finishedCount > 0)
                return "InProgress";

            return "Generated";
        }

        private static string GetTeamName(
            Guid tournamentTeamId,
            IReadOnlyDictionary<Guid, TournamentTeamDto> tournamentTeamsById,
            IReadOnlyDictionary<Guid, TeamDto> teamsById)
        {
            if (!tournamentTeamsById.TryGetValue(tournamentTeamId, out var tournamentTeam))
                return "Unknown";

            return teamsById.TryGetValue(tournamentTeam.TeamId, out var team) ? team.Name : "Unknown";
        }
    }
}

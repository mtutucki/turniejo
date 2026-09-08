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

            var upcomingMatches = matches
                .Where(m => m.Status is "Scheduled" or "Ready")
                .OrderBy(m => m.ScheduledAt)
                .Take(request.UpcomingMatchesCount)
                .ToList();

            var upcomingMatchDtos = new List<UpcomingMatchDto>();
            foreach (var match in upcomingMatches)
            {
                var homeTeamName = await GetTeamNameAsync(match.HomeTournamentTeamId, cancellationToken);
                var awayTeamName = await GetTeamNameAsync(match.AwayTournamentTeamId, cancellationToken);
                upcomingMatchDtos.Add(new UpcomingMatchDto(match.Id, match.ScheduledAt, match.Status, homeTeamName, awayTeamName));
            }

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

        private async Task<string> GetTeamNameAsync(Guid tournamentTeamId, CancellationToken cancellationToken)
        {
            var tournamentTeam = await teamModule.GetTournamentTeamAsync(tournamentTeamId, cancellationToken);
            if (tournamentTeam == null)
                return "Unknown";

            var team = await teamModule.GetTeamAsync(tournamentTeam.TeamId, cancellationToken);
            return team?.Name ?? "Unknown";
        }
    }
}

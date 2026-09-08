using Turniejo.Matches.Application.Abstractions;
using Turniejo.Matches.Application.DTOs;
using Turniejo.SharedKernel.Contracts;
using Turniejo.SharedKernel.CQRS;
using Turniejo.SharedKernel.Results;

namespace Turniejo.Matches.Application.Matches.GetMatchSummary
{
    internal sealed class GetMatchSummaryQueryHandler(
        IMatchesDbContext dbContext,
        ITeamModule teamModule) : IQueryHandler<GetMatchSummaryQuery, MatchSummaryDto>
    {
        public async Task<Result<MatchSummaryDto>> Handle(GetMatchSummaryQuery request, CancellationToken cancellationToken)
        {
            var match = await dbContext.GetMatchByIdAsync(request.MatchId, cancellationToken);
            if (match == null || match.TournamentId != request.TournamentId)
                return Result.Failure<MatchSummaryDto>(Error.NotFound("Match", request.MatchId));

            var homeTeamName = await GetTeamNameAsync(match.HomeTournamentTeamId, cancellationToken);
            var awayTeamName = await GetTeamNameAsync(match.AwayTournamentTeamId, cancellationToken);

            var dto = new MatchSummaryDto(
                match.Id, match.TournamentId, match.Status.ToString(), homeTeamName, awayTeamName,
                match.HomeScore, match.AwayScore, match.ResultType.ToString(), match.WinnerTournamentTeamId, match.FinishedAt);

            return Result.Success(dto);
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

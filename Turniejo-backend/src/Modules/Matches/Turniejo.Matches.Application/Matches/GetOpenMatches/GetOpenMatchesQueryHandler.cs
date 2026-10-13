using Turniejo.Matches.Application.Abstractions;
using Turniejo.Matches.Application.DTOs;
using Turniejo.Matches.Domain.Enums;
using Turniejo.SharedKernel.Contracts;
using Turniejo.SharedKernel.CQRS;
using Turniejo.SharedKernel.Enums;
using Turniejo.SharedKernel.Results;

namespace Turniejo.Matches.Application.Matches.GetOpenMatches
{
    // Pula "wolnych meczów": mecze bez sędziego w turniejach, do których użytkownik dołączył kodem
    // sędziego. Mecz z przypisanym sędzią widzi już tylko ten sędzia (GetAssignedMatchesQuery).
    internal sealed class GetOpenMatchesQueryHandler(
        IMatchesDbContext dbContext,
        IUserModule userModule,
        ICurrentUserContext currentUserContext) : IQueryHandler<GetOpenMatchesQuery, IReadOnlyList<MatchDto>>
    {
        public async Task<Result<IReadOnlyList<MatchDto>>> Handle(GetOpenMatchesQuery request, CancellationToken cancellationToken)
        {
            var roles = await userModule.GetUserRolesAsync(currentUserContext.UserId, cancellationToken);

            var tournamentIds = roles
                .Where(r => r.Role == nameof(UserRole.Referee) && r.ScopeType == nameof(RoleScopeType.Tournament) && r.ScopeId.HasValue)
                .Select(r => r.ScopeId!.Value)
                .Distinct()
                .ToList();

            var dtos = new List<MatchDto>();
            foreach (var tournamentId in tournamentIds)
            {
                var matches = await dbContext.GetMatchesByTournamentIdAsync(tournamentId, cancellationToken);

                dtos.AddRange(matches
                    .Where(m => !m.RefereeId.HasValue && m.Status is not (MatchStatus.Finished or MatchStatus.Cancelled))
                    .Select(m => new MatchDto(
                        m.Id, m.TournamentId, m.CategoryId, m.GroupId, m.PitchId,
                        m.HomeTournamentTeamId, m.AwayTournamentTeamId, m.RefereeId, m.ScheduledAt,
                        m.Status.ToString(), m.StageType.ToString(), m.HomeScore, m.AwayScore, m.ResultType.ToString(),
                        m.HomePenaltyScore, m.AwayPenaltyScore)));
            }

            return Result.Success<IReadOnlyList<MatchDto>>(dtos.OrderBy(m => m.ScheduledAt).ToList());
        }
    }
}

using Turniejo.Matches.Application.Abstractions;
using Turniejo.Matches.Application.DTOs;
using Turniejo.SharedKernel.Contracts;
using Turniejo.SharedKernel.CQRS;
using Turniejo.SharedKernel.Enums;
using Turniejo.SharedKernel.Results;

namespace Turniejo.Matches.Application.Matches.GetAssignedMatches
{
    // Lista "moich meczów" musi iść przez UserRoleAssignment (źródło prawdy dla autoryzacji), a nie
    // przez denormalizowane Match.RefereeId — dziś oba pola są zawsze zgodne tylko dlatego, że jedyna
    // droga nadania roli sędziego (AssignRefereeToMatchCommandHandler) ustawia je razem. Każda inna
    // ścieżka nadania UserRoleAssignment(Referee, Match, X) w przyszłości ominęłaby Match.RefereeId
    // i sędzia nie zobaczyłby swojego meczu na liście.
    internal sealed class GetAssignedMatchesQueryHandler(
        IMatchesDbContext dbContext,
        IUserModule userModule,
        ICurrentUserContext currentUserContext) : IQueryHandler<GetAssignedMatchesQuery, IReadOnlyList<MatchDto>>
    {
        public async Task<Result<IReadOnlyList<MatchDto>>> Handle(GetAssignedMatchesQuery request, CancellationToken cancellationToken)
        {
            var roles = await userModule.GetUserRolesAsync(currentUserContext.UserId, cancellationToken);

            var assignedMatchIds = roles
                .Where(r => r.Role == nameof(UserRole.Referee) && r.ScopeType == nameof(RoleScopeType.Match) && r.ScopeId.HasValue)
                .Select(r => r.ScopeId!.Value)
                .Distinct()
                .ToList();

            if (assignedMatchIds.Count == 0)
                return Result.Success<IReadOnlyList<MatchDto>>([]);

            var matches = await dbContext.GetMatchesByIdsAsync(assignedMatchIds, cancellationToken);

            var dtos = matches
                .OrderBy(m => m.ScheduledAt)
                .Select(m => new MatchDto(
                    m.Id, m.TournamentId, m.CategoryId, m.GroupId, m.PitchId,
                    m.HomeTournamentTeamId, m.AwayTournamentTeamId, m.RefereeId, m.ScheduledAt,
                    m.Status.ToString(), m.StageType.ToString(), m.HomeScore, m.AwayScore, m.ResultType.ToString(),
                    m.HomePenaltyScore, m.AwayPenaltyScore, AwaitingDecision: m.IsAwaitingDecision))
                .ToList();

            return Result.Success<IReadOnlyList<MatchDto>>(dtos);
        }
    }
}

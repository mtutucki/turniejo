using Turniejo.SharedKernel.CQRS;
using Turniejo.SharedKernel.Enums;
using Turniejo.SharedKernel.Results;
using Turniejo.Users.Application.Abstractions;

namespace Turniejo.Users.Application.Users.GetTournamentMembers;

internal sealed class GetTournamentMembersQueryHandler(IUsersDbContext dbContext)
    : IQueryHandler<GetTournamentMembersQuery, IReadOnlyList<TournamentMemberDto>>
{
    public async Task<Result<IReadOnlyList<TournamentMemberDto>>> Handle(
        GetTournamentMembersQuery request,
        CancellationToken cancellationToken)
    {
        var assignments = await dbContext.GetUserRoleAssignmentsByScopeAsync(
            RoleScopeType.Tournament, request.TournamentId, cancellationToken);

        var members = new List<TournamentMemberDto>();
        foreach (var userAssignments in assignments.GroupBy(a => a.UserId))
        {
            var user = await dbContext.GetUserByIdAsync(userAssignments.Key, cancellationToken);
            if (user is null)
                continue;

            members.Add(new TournamentMemberDto(
                user.Id,
                user.Email,
                user.FirstName,
                user.LastName,
                userAssignments.Select(a => a.Role.ToString()).Distinct().ToList()));
        }

        return Result.Success<IReadOnlyList<TournamentMemberDto>>(
            members.OrderBy(m => m.LastName).ThenBy(m => m.FirstName).ToList());
    }
}

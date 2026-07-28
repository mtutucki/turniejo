using Turniejo.SharedKernel.CQRS;
using Turniejo.SharedKernel.Results;
using Turniejo.Users.Application.Abstractions;

namespace Turniejo.Users.Application.Users.GetUserRoles;

internal sealed class GetUserRolesQueryHandler(
    IUsersDbContext dbContext)
    : IQueryHandler<GetUserRolesQuery, IReadOnlyList<UserRoleDto>>
{
    public async Task<Result<IReadOnlyList<UserRoleDto>>> Handle(
        GetUserRolesQuery request,
        CancellationToken cancellationToken)
    {
        var roles = await dbContext.GetUserRolesAsync(
            request.UserId, cancellationToken);

        var dtos = roles.Select(r => new UserRoleDto(
            r.Id,
            r.UserId,
            r.Role.ToString(),
            r.ScopeType.ToString(),
            r.ScopeId,
            r.CreatedAt)).ToList();

        return Result.Success<IReadOnlyList<UserRoleDto>>(dtos);
    }
}
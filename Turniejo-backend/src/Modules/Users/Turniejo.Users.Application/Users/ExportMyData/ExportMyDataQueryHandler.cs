using Turniejo.SharedKernel.Contracts;
using Turniejo.SharedKernel.CQRS;
using Turniejo.SharedKernel.Results;
using Turniejo.Users.Application.Abstractions;

namespace Turniejo.Users.Application.Users.ExportMyData;

internal sealed class ExportMyDataQueryHandler(
    IUsersDbContext dbContext,
    ICurrentUserContext currentUserContext,
    IPlayerModule playerModule) : IQueryHandler<ExportMyDataQuery, MyDataExportDto>
{
    public async Task<Result<MyDataExportDto>> Handle(ExportMyDataQuery request, CancellationToken cancellationToken)
    {
        var userId = currentUserContext.UserId;
        var user = await dbContext.GetUserByIdAsync(userId, cancellationToken);
        if (user is null)
            return Result.Failure<MyDataExportDto>(Error.NotFound("User", userId));

        var roles = await dbContext.GetUserRolesAsync(userId, cancellationToken);
        var children = await playerModule.GetPlayersByGuardianAsync(userId, cancellationToken);

        return Result.Success(new MyDataExportDto(
            DateTimeOffset.UtcNow,
            new MyProfileExportDto(
                user.Id, user.Email, user.FirstName, user.LastName, user.PhoneNumber,
                user.CreatedAt, user.TermsAcceptedAt, user.TermsVersion),
            roles.Select(r => new MyRoleExportDto(r.Role.ToString(), r.ScopeType.ToString(), r.ScopeId, r.CreatedAt)).ToList(),
            children.Select(c => new MyChildExportDto(c.Id, c.FirstName, c.LastName)).ToList()));
    }
}

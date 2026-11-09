using Turniejo.SharedKernel.Contracts;
using Turniejo.SharedKernel.CQRS;
using Turniejo.SharedKernel.Results;
using Turniejo.Users.Application.Abstractions;

namespace Turniejo.Users.Application.Users.GetMyProfile;

internal sealed class GetMyProfileQueryHandler(
    IUsersDbContext dbContext,
    ICurrentUserContext currentUserContext) : IQueryHandler<GetMyProfileQuery, MyProfileDto>
{
    public async Task<Result<MyProfileDto>> Handle(GetMyProfileQuery request, CancellationToken cancellationToken)
    {
        var user = await dbContext.GetUserByIdAsync(currentUserContext.UserId, cancellationToken);
        if (user is null)
            return Result.Failure<MyProfileDto>(Error.NotFound("User", currentUserContext.UserId));

        return Result.Success(new MyProfileDto(user.Email, user.FirstName, user.LastName, user.PhoneNumber, user.SeenReleaseVersion));
    }
}

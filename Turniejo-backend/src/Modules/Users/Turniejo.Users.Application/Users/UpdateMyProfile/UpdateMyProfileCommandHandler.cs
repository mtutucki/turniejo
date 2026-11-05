using Turniejo.SharedKernel.Contracts;
using Turniejo.SharedKernel.CQRS;
using Turniejo.SharedKernel.Results;
using Turniejo.Users.Application.Abstractions;

namespace Turniejo.Users.Application.Users.UpdateMyProfile;

internal sealed class UpdateMyProfileCommandHandler(
    IUsersDbContext dbContext,
    IKeycloakAdminService keycloakAdminService,
    ICurrentUserContext currentUserContext) : ICommandHandler<UpdateMyProfileCommand>
{
    public async Task<Result> Handle(UpdateMyProfileCommand request, CancellationToken cancellationToken)
    {
        var user = await dbContext.GetUserByIdAsync(currentUserContext.UserId, cancellationToken);
        if (user is null)
            return Result.Failure(Error.NotFound("User", currentUserContext.UserId));

        var result = user.UpdateProfile(request.FirstName, request.LastName, request.PhoneNumber);
        if (result.IsFailure)
            return result;

        // Najpierw Keycloak (imię i nazwisko w tokenie), potem zapis: błąd Keycloaka nie zostawi
        // profilu w bazie rozjechanego z kontem logowania.
        await keycloakAdminService.UpdateNameAsync(user.Id, user.FirstName, user.LastName, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}

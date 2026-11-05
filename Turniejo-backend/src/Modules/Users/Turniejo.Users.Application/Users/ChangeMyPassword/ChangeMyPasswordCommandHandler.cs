using Microsoft.Extensions.Logging;
using Turniejo.SharedKernel.Contracts;
using Turniejo.SharedKernel.CQRS;
using Turniejo.SharedKernel.Results;
using Turniejo.Users.Application.Abstractions;
using Turniejo.Users.Domain.Enums;

namespace Turniejo.Users.Application.Users.ChangeMyPassword;

internal sealed class ChangeMyPasswordCommandHandler(
    IUsersDbContext dbContext,
    IKeycloakAdminService keycloakAdminService,
    ICurrentUserContext currentUserContext,
    ILogger<ChangeMyPasswordCommandHandler> logger) : ICommandHandler<ChangeMyPasswordCommand>
{
    public async Task<Result> Handle(ChangeMyPasswordCommand request, CancellationToken cancellationToken)
    {
        var user = await dbContext.GetUserByIdAsync(currentUserContext.UserId, cancellationToken);
        if (user is null || user.Status != UserStatus.Active)
            return Result.Failure(Error.NotFound("User", currentUserContext.UserId));

        if (!await keycloakAdminService.VerifyPasswordAsync(user.Email, request.CurrentPassword, cancellationToken))
            return Result.Failure(Error.Validation("CurrentPassword", "Current password is incorrect."));

        await keycloakAdminService.SetPasswordAsync(user.Id, request.NewPassword, cancellationToken);

        try
        {
            // Jak przy resecie: zmiana hasła bywa reakcją na przejęcie konta, więc stare sesje się kończą.
            // Frontend od razu loguje usera nowym hasłem, więc on sam nie traci sesji.
            await keycloakAdminService.LogoutAllSessionsAsync(user.Id, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to close sessions after password change for user {UserId}", user.Id);
        }

        return Result.Success();
    }
}

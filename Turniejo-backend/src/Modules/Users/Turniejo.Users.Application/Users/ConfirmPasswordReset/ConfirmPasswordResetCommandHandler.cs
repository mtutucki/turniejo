using Microsoft.Extensions.Logging;
using Turniejo.SharedKernel.Contracts;
using Turniejo.SharedKernel.CQRS;
using Turniejo.SharedKernel.Results;
using Turniejo.Users.Application.Abstractions;
using Turniejo.Users.Application.Users.RequestPasswordReset;
using Turniejo.Users.Domain.Enums;

namespace Turniejo.Users.Application.Users.ConfirmPasswordReset;

internal sealed class ConfirmPasswordResetCommandHandler(
    IKeycloakAdminService keycloakAdminService,
    IUsersDbContext dbContext,
    ILogger<ConfirmPasswordResetCommandHandler> logger)
    : ICommandHandler<ConfirmPasswordResetCommand>
{
    // Jeden komunikat dla nieznanego, zużytego i wygasłego linku — nie zdradzamy, który przypadek zaszedł.
    private static readonly Error InvalidToken =
        Error.Validation("Token", "Reset link is invalid or has expired.");

    public async Task<Result> Handle(
        ConfirmPasswordResetCommand request,
        CancellationToken cancellationToken)
    {
        var token = await dbContext.GetPasswordResetTokenByHashAsync(
            PasswordResetTokenGenerator.Hash(request.Token), cancellationToken);

        if (token is null || !token.IsUsable(DateTimeOffset.UtcNow))
            return Result.Failure(InvalidToken);

        var user = await dbContext.GetUserByIdAsync(token.UserId, cancellationToken);
        if (user is null || user.Status != UserStatus.Active)
            return Result.Failure(InvalidToken);

        await keycloakAdminService.SetPasswordAsync(user.Id, request.NewPassword, cancellationToken);

        token.MarkUsed();
        await dbContext.SaveChangesAsync(cancellationToken);

        try
        {
            // Reset hasła bywa reakcją na przejęcie konta — stare sesje nie mogą go przeżyć.
            await keycloakAdminService.LogoutAllSessionsAsync(user.Id, cancellationToken);
        }
        catch (Exception ex)
        {
            // Hasło jest już zmienione, więc user ma dostać sukces; sesje wygasną same.
            logger.LogWarning(ex, "Failed to close sessions after password reset for user {UserId}", user.Id);
        }

        return Result.Success();
    }
}

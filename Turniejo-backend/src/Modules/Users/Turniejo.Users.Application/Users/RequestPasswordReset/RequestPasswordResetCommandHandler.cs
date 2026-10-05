using Microsoft.Extensions.Logging;
using Turniejo.SharedKernel.Contracts;
using Turniejo.SharedKernel.CQRS;
using Turniejo.SharedKernel.Results;
using Turniejo.Users.Application.Abstractions;

namespace Turniejo.Users.Application.Users.RequestPasswordReset;

internal sealed class RequestPasswordResetCommandHandler(
    IKeycloakAdminService keycloakAdminService,
    IUsersDbContext dbContext,
    ILogger<RequestPasswordResetCommandHandler> logger)
    : ICommandHandler<RequestPasswordResetCommand>
{
    public async Task<Result> Handle(
        RequestPasswordResetCommand request,
        CancellationToken cancellationToken)
    {
        // Szukamy usera po emailu żeby dostać Keycloak UserId
        var user = await dbContext.GetUserByEmailAsync(
            request.Email, cancellationToken);

        // Zawsze zwracamy Success — nie ujawniamy czy email istnieje
        if (user is null)
            return Result.Success();

        try
        {
            await keycloakAdminService.SendPasswordResetEmailAsync(
                user.Id, cancellationToken);
        }
        catch (Exception ex)
        {
            // Nie zwracamy błędu userowi (nie ujawniamy czy email istnieje), ale awaria musi zostać
            // zalogowana — inaczej problem z Keycloakiem (np. padnięty SMTP) jest niewidoczny dla
            // nikogo, dopóki użytkownicy nie zaczną się skarżyć, że nie dostają maili resetujących.
            logger.LogError(ex, "Failed to send password reset email for user {UserId}", user.Id);
            return Result.Success();
        }

        return Result.Success();
    }
}
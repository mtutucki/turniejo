using Turniejo.SharedKernel.Contracts;
using Turniejo.SharedKernel.CQRS;
using Turniejo.SharedKernel.Results;
using Turniejo.Users.Application.Abstractions;

namespace Turniejo.Users.Application.Users.RequestPasswordReset;

internal sealed class RequestPasswordResetCommandHandler(
    IKeycloakAdminService keycloakAdminService,
    IUsersDbContext dbContext)
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
        catch (Exception)
        {
            // Logujemy błąd ale nie zwracamy go userowi
            return Result.Success();
        }

        return Result.Success();
    }
}
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Turniejo.SharedKernel.Contracts;
using Turniejo.SharedKernel.CQRS;
using Turniejo.SharedKernel.Results;
using Turniejo.Users.Application.Abstractions;
using Turniejo.Users.Domain.Entities;
using Turniejo.Users.Domain.Enums;

namespace Turniejo.Users.Application.Users.RequestPasswordReset;

internal sealed class RequestPasswordResetCommandHandler(
    IEmailSender emailSender,
    IUsersDbContext dbContext,
    IOptions<AppOptions> appOptions,
    ILogger<RequestPasswordResetCommandHandler> logger)
    : ICommandHandler<RequestPasswordResetCommand>
{
    public async Task<Result> Handle(
        RequestPasswordResetCommand request,
        CancellationToken cancellationToken)
    {
        var user = await dbContext.GetUserByEmailAsync(
            request.Email, cancellationToken);

        // Zawsze zwracamy Success — nie ujawniamy czy email istnieje
        if (user is null || user.Status != UserStatus.Active)
            return Result.Success();

        try
        {
            var (token, tokenHash) = PasswordResetTokenGenerator.Generate();

            // Nowy link unieważnia poprzednie — działa zawsze tylko ostatnio wysłany.
            var previousTokens = await dbContext.GetPasswordResetTokensAsync(user.Id, cancellationToken);
            dbContext.RemovePasswordResetTokens(previousTokens);
            await dbContext.AddPasswordResetTokenAsync(PasswordResetToken.Create(user.Id, tokenHash), cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);

            var link = $"{appOptions.Value.PublicUrl.TrimEnd('/')}/password-reset/confirm?token={token}";
            await emailSender.SendAsync(PasswordResetEmail.Create(user, link), cancellationToken);
        }
        catch (Exception ex)
        {
            // Nie zwracamy błędu userowi (nie ujawniamy czy email istnieje), ale awaria musi zostać
            // zalogowana — inaczej problem z wysyłką (np. padnięty SMTP) jest niewidoczny dla
            // nikogo, dopóki użytkownicy nie zaczną się skarżyć, że nie dostają maili resetujących.
            logger.LogError(ex, "Failed to send password reset email for user {UserId}", user.Id);
            return Result.Success();
        }

        return Result.Success();
    }
}

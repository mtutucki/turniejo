using Turniejo.SharedKernel.Contracts;
using Turniejo.SharedKernel.CQRS;
using Turniejo.SharedKernel.Results;
using Turniejo.Users.Application.Abstractions;

namespace Turniejo.Users.Application.Users.SetMyTheme;

internal sealed class SetMyThemeCommandHandler(
    IUsersDbContext dbContext,
    ICurrentUserContext currentUserContext) : ICommandHandler<SetMyThemeCommand>
{
    public async Task<Result> Handle(SetMyThemeCommand request, CancellationToken cancellationToken)
    {
        if (!ThemeNames.TryParse(request.Theme, out var theme))
            return Result.Failure(Error.Validation("Theme", "Theme must be system, light or dark."));

        var user = await dbContext.GetUserByIdAsync(currentUserContext.UserId, cancellationToken);
        if (user is null)
            return Result.Failure(Error.NotFound("User", currentUserContext.UserId));

        var result = user.SetThemePreference(theme);
        if (result.IsFailure)
            return result;

        await dbContext.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}

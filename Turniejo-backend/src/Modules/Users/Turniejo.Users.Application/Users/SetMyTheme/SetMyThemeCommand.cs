using Turniejo.SharedKernel.CQRS;

namespace Turniejo.Users.Application.Users.SetMyTheme;

// Motyw na koncie z ICurrentUserContext.UserId: "system", "light" albo "dark".
public sealed record SetMyThemeCommand(string Theme) : ICommand, IAuthenticatedOnlyRequest;

using Turniejo.SharedKernel.CQRS;

namespace Turniejo.Users.Application.Users.DeleteMyAccount;

// Handler działa wyłącznie na koncie z ICurrentUserContext.UserId — brak parametrów wejściowych.
public sealed record DeleteMyAccountCommand : ICommand, IAuthenticatedOnlyRequest;

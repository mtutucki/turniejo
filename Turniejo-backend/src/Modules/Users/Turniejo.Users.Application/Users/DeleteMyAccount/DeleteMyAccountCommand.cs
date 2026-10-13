using Turniejo.SharedKernel.CQRS;

namespace Turniejo.Users.Application.Users.DeleteMyAccount;

// Handler działa wyłącznie na koncie z ICurrentUserContext.UserId. DeleteOrganization to świadoma zgoda
// jedynego organizatora na usunięcie organizacji razem z kontem — bez niej handler zwraca 409.
public sealed record DeleteMyAccountCommand(bool DeleteOrganization = false) : ICommand, IAuthenticatedOnlyRequest;

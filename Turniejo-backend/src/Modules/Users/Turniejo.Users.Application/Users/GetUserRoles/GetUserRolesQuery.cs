using Turniejo.SharedKernel.CQRS;

namespace Turniejo.Users.Application.Users.GetUserRoles;

// Martwy kod dziś — brak kontrolera, brak wywołania nawet z IUserModule.GetUserRolesAsync (ten woła
// dbContext bezpośrednio). IAuthenticatedOnlyRequest to świadomie MINIMUM: jeśli to kiedyś zostanie
// wystawione przez API, zwraca role DOWOLNEGO UserId, więc będzie potrzebować prawdziwej autoryzacji
// (np. self-only albo Organizer), nie tylko "zalogowany" — nie traktować tego markera jako "gotowe".
public sealed record GetUserRolesQuery(Guid UserId)
    : IQuery<IReadOnlyList<UserRoleDto>>, IAuthenticatedOnlyRequest;

public sealed record UserRoleDto(
    Guid Id,
    Guid UserId,
    string Role,
    string ScopeType,
    Guid? ScopeId,
    DateTimeOffset CreatedAt);
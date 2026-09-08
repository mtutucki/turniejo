using Turniejo.SharedKernel.CQRS;
using Turniejo.SharedKernel.Enums;

namespace Turniejo.Users.Application.Users.AssignUserRole;

// Wołane wyłącznie wewnętrznie przez IUserModule.AssignRoleAsync — nigdy z kontrolera. Autoryzacja
// "kto może nadać jaką rolę" jest egzekwowana przez IAuthorizedRequest komendy WYWOŁUJĄCEJ (np.
// CreateTournamentCommand wymaga Organizer), a nie tutaj — stąd IPublicRequest, żeby to działało
// też z kontekstów bez zalogowanego usera (Outbox handlers, np. UserRegisteredHandler).
public sealed record AssignUserRoleCommand(
    Guid UserId,
    UserRole Role,
    RoleScopeType ScopeType,
    Guid? ScopeId = null) : ICommand, IPublicRequest;
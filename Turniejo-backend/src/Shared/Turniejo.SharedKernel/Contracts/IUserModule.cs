namespace Turniejo.SharedKernel.Contracts;

public interface IUserModule
{
    Task<UserDto?> GetUserAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<UserRoleDto>> GetUserRolesAsync(Guid userId, CancellationToken cancellationToken = default);
    Task AssignRoleAsync(Guid userId, string role, string scopeType, Guid scopeId, CancellationToken cancellationToken = default);
    Task RevokeRoleAsync(Guid userId, string role, string scopeType, Guid scopeId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Guid>> GetUserIdsByScopeAsync(string scopeType, Guid scopeId, CancellationToken cancellationToken = default);
    /// <summary>Usuwa role wszystkich userów w podanych zakresach (np. usuniętych zgłoszeń drużyn), od razu w bazie.</summary>
    Task RemoveRolesByScopesAsync(string scopeType, IReadOnlyCollection<Guid> scopeIds, CancellationToken cancellationToken = default);
}

public record UserDto(Guid Id, string Email, string FirstName, string LastName);
public record UserRoleDto(Guid UserId, string Role, string ScopeType, Guid? ScopeId);
namespace Turniejo.SharedKernel.Contracts;

public interface IUserModule
{
    Task<UserDto?> GetUserAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<UserRoleDto>> GetUserRolesAsync(Guid userId, CancellationToken cancellationToken = default);
    Task AssignRoleAsync(Guid userId, string role, string scopeType, Guid scopeId, CancellationToken cancellationToken = default);
}

public record UserDto(Guid Id, string Email, string FirstName, string LastName);
public record UserRoleDto(Guid UserId, string Role, string ScopeType, Guid? ScopeId);
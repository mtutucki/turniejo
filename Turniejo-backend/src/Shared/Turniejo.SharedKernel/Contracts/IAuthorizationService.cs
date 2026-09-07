namespace Turniejo.SharedKernel.Contracts;

public interface IAuthorizationService
{
    Task<bool> HasAnyRoleAsync(Guid userId, string scopeType, Guid scopeId, IReadOnlyCollection<string> roles, CancellationToken cancellationToken = default);
}
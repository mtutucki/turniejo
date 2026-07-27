namespace Turniejo.SharedKernel.Contracts;

public interface IAuthorizationService
{
    Task<bool> HasRoleAsync(Guid userId, string role, string scopeType, Guid? scopeId, CancellationToken cancellationToken = default);
}
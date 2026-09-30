namespace Turniejo.SharedKernel.Contracts;

public interface IKeycloakAdminService
{
    Task<Guid> CreateUserAsync(
        string email,
        string password,
        string firstName,
        string lastName,
        CancellationToken cancellationToken = default);

    /// <summary>Usuwa konto logowania. Nieistniejące konto (404) to sukces — wywołanie jest idempotentne.</summary>
    Task DeleteUserAsync(
        Guid keycloakUserId,
        CancellationToken cancellationToken = default);

    Task SendPasswordResetEmailAsync(
        Guid keycloakUserId,
        CancellationToken cancellationToken = default);
}
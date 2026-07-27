namespace Turniejo.SharedKernel.Contracts;

public interface IKeycloakAdminService
{
    Task<Guid> CreateUserAsync(
        string email,
        string password,
        string firstName,
        string lastName,
        CancellationToken cancellationToken = default);
}
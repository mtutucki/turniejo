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

    Task SetPasswordAsync(
        Guid keycloakUserId,
        string password,
        CancellationToken cancellationToken = default);

    /// <summary>Imię i nazwisko konta logowania — trafiają do tokenu, więc muszą zgadzać się z profilem.</summary>
    Task UpdateNameAsync(
        Guid keycloakUserId,
        string firstName,
        string lastName,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Sprawdza hasło logowaniem do Keycloaka (bez zostawiania sesji). Zmiana hasła wymaga podania
    /// obecnego, żeby przejęta sesja nie wystarczyła do przejęcia konta.
    /// </summary>
    Task<bool> VerifyPasswordAsync(
        string username,
        string password,
        CancellationToken cancellationToken = default);

    /// <summary>Zamyka wszystkie sesje usera — jego refresh tokeny przestają działać.</summary>
    Task LogoutAllSessionsAsync(
        Guid keycloakUserId,
        CancellationToken cancellationToken = default);
}
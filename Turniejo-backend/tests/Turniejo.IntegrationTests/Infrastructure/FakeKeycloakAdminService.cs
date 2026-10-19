using System.Collections.Concurrent;
using Turniejo.SharedKernel.Contracts;

namespace Turniejo.IntegrationTests.Infrastructure;

// Rejestracja usera (RegisterUserCommandHandler) woła prawdziwe Keycloak Admin API — testy
// integracyjne mają zweryfikować NASZ kod (HTTP -> handler -> EF -> Postgres), nie ponownie
// weryfikować zewnętrznego Keycloaka, więc podmieniamy tę jedną zależność na deterministyczny fake.
public sealed class FakeKeycloakAdminService : IKeycloakAdminService
{
    private readonly ConcurrentDictionary<string, Guid> _usersByEmail = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<Guid, string> _passwords = new();
    public ConcurrentBag<(Guid UserId, string Password)> PasswordChanges { get; } = [];
    public ConcurrentBag<(Guid UserId, string FirstName, string LastName)> NameChanges { get; } = [];
    public ConcurrentBag<Guid> LoggedOutUsers { get; } = [];
    public ConcurrentBag<Guid> DeletedUsers { get; } = [];

    public Task<Guid> CreateUserAsync(
        string email, string password, string firstName, string lastName, CancellationToken cancellationToken = default)
    {
        if (_usersByEmail.ContainsKey(email))
            throw new InvalidOperationException("CONFLICT: user already exists");

        var id = Guid.NewGuid();
        _usersByEmail[email] = id;
        _passwords[id] = password;
        return Task.FromResult(id);
    }

    public Task SetPasswordAsync(Guid keycloakUserId, string password, CancellationToken cancellationToken = default)
    {
        PasswordChanges.Add((keycloakUserId, password));
        _passwords[keycloakUserId] = password;
        return Task.CompletedTask;
    }

    public Task UpdateNameAsync(Guid keycloakUserId, string firstName, string lastName, CancellationToken cancellationToken = default)
    {
        NameChanges.Add((keycloakUserId, firstName, lastName));
        return Task.CompletedTask;
    }

    public Task<bool> VerifyPasswordAsync(string username, string password, CancellationToken cancellationToken = default) =>
        Task.FromResult(
            _usersByEmail.TryGetValue(username, out var id)
            && _passwords.TryGetValue(id, out var stored)
            && stored == password);

    public Task LogoutAllSessionsAsync(Guid keycloakUserId, CancellationToken cancellationToken = default)
    {
        LoggedOutUsers.Add(keycloakUserId);
        return Task.CompletedTask;
    }

    public Task DeleteUserAsync(Guid keycloakUserId, CancellationToken cancellationToken = default)
    {
        foreach (var (email, id) in _usersByEmail)
        {
            if (id == keycloakUserId)
                _usersByEmail.TryRemove(email, out _);
        }

        DeletedUsers.Add(keycloakUserId);
        return Task.CompletedTask;
    }

    public void Reset()
    {
        _usersByEmail.Clear();
        _passwords.Clear();
        PasswordChanges.Clear();
        NameChanges.Clear();
        LoggedOutUsers.Clear();
        DeletedUsers.Clear();
    }
}

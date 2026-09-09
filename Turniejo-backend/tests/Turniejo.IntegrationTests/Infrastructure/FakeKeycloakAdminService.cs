using System.Collections.Concurrent;
using Turniejo.SharedKernel.Contracts;

namespace Turniejo.IntegrationTests.Infrastructure;

// Rejestracja usera (RegisterUserCommandHandler) woła prawdziwe Keycloak Admin API — testy
// integracyjne mają zweryfikować NASZ kod (HTTP -> handler -> EF -> Postgres), nie ponownie
// weryfikować zewnętrznego Keycloaka, więc podmieniamy tę jedną zależność na deterministyczny fake.
public sealed class FakeKeycloakAdminService : IKeycloakAdminService
{
    private readonly ConcurrentDictionary<string, Guid> _usersByEmail = new(StringComparer.OrdinalIgnoreCase);
    public ConcurrentBag<Guid> PasswordResetRequests { get; } = [];

    public Task<Guid> CreateUserAsync(
        string email, string password, string firstName, string lastName, CancellationToken cancellationToken = default)
    {
        if (_usersByEmail.ContainsKey(email))
            throw new InvalidOperationException("CONFLICT: user already exists");

        var id = Guid.NewGuid();
        _usersByEmail[email] = id;
        return Task.FromResult(id);
    }

    public Task SendPasswordResetEmailAsync(Guid keycloakUserId, CancellationToken cancellationToken = default)
    {
        PasswordResetRequests.Add(keycloakUserId);
        return Task.CompletedTask;
    }

    public void Reset()
    {
        _usersByEmail.Clear();
        PasswordResetRequests.Clear();
    }
}

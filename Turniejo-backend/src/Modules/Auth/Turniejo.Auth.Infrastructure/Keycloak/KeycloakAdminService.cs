using Microsoft.Extensions.Configuration;
using System.Net.Http.Json;
using Turniejo.SharedKernel.Contracts;

namespace Turniejo.Auth.Infrastructure.Keycloak;

internal sealed class KeycloakAdminService(
    HttpClient httpClient,
    IConfiguration configuration) : IKeycloakAdminService
{
    public async Task<Guid> CreateUserAsync(
        string email,
        string password,
        string firstName,
        string lastName,
        CancellationToken cancellationToken = default)
    {
        var request = new KeycloakUserRequest(
            Username: email,
            Email: email,
            FirstName: firstName,
            LastName: lastName,
            Enabled: true,
            EmailVerified: true,
            Credentials:
            [
                new KeycloakCredential("password", password, Temporary: false)
            ]);

        var realm = configuration["Keycloak:Realm"];
        var response = await httpClient.PostAsJsonAsync(
            $"/admin/realms/{realm}/users",
            request,
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(cancellationToken);

            if (response.StatusCode == System.Net.HttpStatusCode.Conflict)
                throw new InvalidOperationException("CONFLICT: User exists with same email.");

            throw new InvalidOperationException(
                $"Keycloak user creation failed: {response.StatusCode} - {error}");
        }

        var location = response.Headers.Location?.ToString()
            ?? throw new InvalidOperationException("Keycloak did not return user location.");

        var userId = location.Split('/').Last();
        return Guid.Parse(userId);
    }
}
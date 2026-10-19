using Microsoft.Extensions.Configuration;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Turniejo.SharedKernel.Contracts;

namespace Turniejo.Auth.Infrastructure.Keycloak;

internal sealed class KeycloakAdminService(
    HttpClient httpClient,
    IHttpClientFactory httpClientFactory,
    IConfiguration configuration) : IKeycloakAdminService
{
    public const string PasswordCheckHttpClientName = "KeycloakPasswordCheck";

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

    public async Task DeleteUserAsync(
        Guid keycloakUserId,
        CancellationToken cancellationToken = default)
    {
        var realm = configuration["Keycloak:Realm"];
        var response = await httpClient.DeleteAsync(
            $"/admin/realms/{realm}/users/{keycloakUserId}",
            cancellationToken);

        if (!response.IsSuccessStatusCode && response.StatusCode != System.Net.HttpStatusCode.NotFound)
        {
            var error = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new InvalidOperationException(
                $"Keycloak user deletion failed: {response.StatusCode} - {error}");
        }
    }

    public async Task SetPasswordAsync(
        Guid keycloakUserId,
        string password,
        CancellationToken cancellationToken = default)
    {
        var realm = configuration["Keycloak:Realm"];
        var response = await httpClient.PutAsJsonAsync(
            $"/admin/realms/{realm}/users/{keycloakUserId}/reset-password",
            new KeycloakCredential("password", password, Temporary: false),
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new InvalidOperationException(
                $"Keycloak password change failed: {response.StatusCode} - {error}");
        }
    }

    public async Task UpdateNameAsync(
        Guid keycloakUserId,
        string firstName,
        string lastName,
        CancellationToken cancellationToken = default)
    {
        var realm = configuration["Keycloak:Realm"];
        var path = $"/admin/realms/{realm}/users/{keycloakUserId}";

        // Pełna reprezentacja, nie sam firstName/lastName: z włączonym User Profile Keycloak przy PUT
        // potrafi wyczyścić atrybuty, których nie ma w body.
        var user = await httpClient.GetFromJsonAsync<JsonObject>(path, cancellationToken)
            ?? throw new InvalidOperationException("Keycloak returned an empty user.");
        user["firstName"] = firstName;
        user["lastName"] = lastName;

        var response = await httpClient.PutAsJsonAsync(path, user, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new InvalidOperationException(
                $"Keycloak user update failed: {response.StatusCode} - {error}");
        }
    }

    public async Task<bool> VerifyPasswordAsync(
        string username,
        string password,
        CancellationToken cancellationToken = default)
    {
        var realm = configuration["Keycloak:Realm"];
        var clientId = configuration["Keycloak:PasswordCheckClientId"];
        if (string.IsNullOrWhiteSpace(clientId))
            throw new InvalidOperationException("Keycloak:PasswordCheckClientId must be configured.");

        // Osobny klient bez KeycloakAdminTokenHandler: endpoint tokenów nie przyjmuje tokenu admina.
        var client = httpClientFactory.CreateClient(PasswordCheckHttpClientName);
        var tokenPath = $"/realms/{realm}/protocol/openid-connect/token";
        var response = await client.PostAsync(tokenPath, new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "password",
            ["client_id"] = clientId,
            ["username"] = username,
            ["password"] = password,
        }), cancellationToken);

        // Złe hasło to 401 invalid_grant; 400 to m.in. konto z niedokończoną akcją (np. reset hasła).
        if (response.StatusCode is System.Net.HttpStatusCode.Unauthorized or System.Net.HttpStatusCode.BadRequest)
            return false;

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new InvalidOperationException(
                $"Keycloak password check failed: {response.StatusCode} - {error}");
        }

        // Logowanie kontrolne otworzyło sesję w Keycloaku — zamykamy ją od razu.
        var tokens = await response.Content.ReadFromJsonAsync<JsonObject>(cancellationToken);
        var refreshToken = tokens?["refresh_token"]?.GetValue<string>();
        if (refreshToken is not null)
        {
            await client.PostAsync($"/realms/{realm}/protocol/openid-connect/logout", new FormUrlEncodedContent(
                new Dictionary<string, string> { ["client_id"] = clientId, ["refresh_token"] = refreshToken }),
                cancellationToken);
        }

        return true;
    }

    public async Task LogoutAllSessionsAsync(
        Guid keycloakUserId,
        CancellationToken cancellationToken = default)
    {
        var realm = configuration["Keycloak:Realm"];
        var response = await httpClient.PostAsync(
            $"/admin/realms/{realm}/users/{keycloakUserId}/logout",
            content: null,
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new InvalidOperationException(
                $"Keycloak session logout failed: {response.StatusCode} - {error}");
        }
    }
}
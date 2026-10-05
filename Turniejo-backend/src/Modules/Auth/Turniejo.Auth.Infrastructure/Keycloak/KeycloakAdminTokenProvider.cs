using Microsoft.Extensions.Configuration;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace Turniejo.Auth.Infrastructure.Keycloak;

// Token service accounta klienta Keycloak:AdminClientId (grant client_credentials) w realmie aplikacji.
// Wcześniej backend logował się hasłem admina realmu master — wyciek tego hasła dawał pełną kontrolę
// nad całym Keycloakiem. Service account ma tylko realm-management/manage-users + view-users, czyli
// dokładnie to, czego potrzebuje KeycloakAdminService (tworzenie/usuwanie userów, mail z resetem hasła).
//
// Singleton z cache — token żyje kilka minut, a bez cache każde wywołanie Admin API kosztowało
// dodatkowy round-trip do Keycloaka.
internal sealed class KeycloakAdminTokenProvider(
    IHttpClientFactory httpClientFactory,
    IConfiguration configuration)
{
    public const string HttpClientName = "KeycloakAdminToken";

    // Odświeżamy z zapasem, żeby token nie wygasł między pobraniem z cache a dotarciem requestu do Keycloaka.
    private static readonly TimeSpan RefreshMargin = TimeSpan.FromSeconds(30);

    private readonly SemaphoreSlim _lock = new(1, 1);
    private string? _accessToken;
    private DateTimeOffset _expiresAt;

    public async Task<string> GetAccessTokenAsync(CancellationToken cancellationToken)
    {
        if (_accessToken is not null && DateTimeOffset.UtcNow < _expiresAt - RefreshMargin)
            return _accessToken;

        await _lock.WaitAsync(cancellationToken);
        try
        {
            if (_accessToken is not null && DateTimeOffset.UtcNow < _expiresAt - RefreshMargin)
                return _accessToken;

            var token = await RequestTokenAsync(cancellationToken);
            _accessToken = token.AccessToken;
            _expiresAt = DateTimeOffset.UtcNow.AddSeconds(token.ExpiresIn);
            return _accessToken;
        }
        finally
        {
            _lock.Release();
        }
    }

    private async Task<TokenResponse> RequestTokenAsync(CancellationToken cancellationToken)
    {
        var realm = configuration["Keycloak:Realm"];
        var clientId = configuration["Keycloak:AdminClientId"];
        var clientSecret = configuration["Keycloak:AdminClientSecret"];

        // Brak sekretu na produkcji ma wywrócić request od razu, z czytelnym komunikatem — a nie
        // skończyć się 401 z Keycloaka, który wygląda jak błąd po stronie usera.
        if (string.IsNullOrWhiteSpace(realm) || string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(clientSecret))
            throw new InvalidOperationException(
                "Keycloak:Realm, Keycloak:AdminClientId and Keycloak:AdminClientSecret must be configured.");

        var client = httpClientFactory.CreateClient(HttpClientName);
        using var response = await client.PostAsync(
            $"/realms/{realm}/protocol/openid-connect/token",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "client_credentials",
                ["client_id"] = clientId,
                ["client_secret"] = clientSecret
            }),
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new InvalidOperationException(
                $"Failed to get Keycloak admin token: {response.StatusCode} - {error}");
        }

        var token = await response.Content.ReadFromJsonAsync<TokenResponse>(cancellationToken);
        if (string.IsNullOrEmpty(token?.AccessToken))
            throw new InvalidOperationException("Keycloak token response did not contain an access token.");

        return token;
    }

    private sealed record TokenResponse(
        [property: JsonPropertyName("access_token")] string AccessToken,
        [property: JsonPropertyName("expires_in")] int ExpiresIn);
}

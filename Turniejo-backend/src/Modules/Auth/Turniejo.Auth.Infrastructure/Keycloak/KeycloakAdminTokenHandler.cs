using Microsoft.Extensions.Configuration;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace Turniejo.Auth.Infrastructure.Keycloak;

internal sealed class KeycloakAdminTokenHandler(IConfiguration configuration)
    : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var token = await GetAdminTokenAsync(cancellationToken);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await base.SendAsync(request, cancellationToken);
    }

    private async Task<string> GetAdminTokenAsync(CancellationToken cancellationToken)
    {
        var realm = configuration["Keycloak:AdminRealm"] ?? "master";
        var clientId = configuration["Keycloak:AdminClientId"] ?? "admin-cli";
        var username = configuration["Keycloak:AdminUsername"];
        var password = configuration["Keycloak:AdminPassword"];
        var baseUrl = configuration["Keycloak:BaseUrl"];

        // Bez tego brak zmiennej środowiskowej/sekretu na produkcji cicho "fail-open"owało do
        // zgadywalnego admin/admin zamiast rzucić błąd konfiguracji — realm/client-id to publiczne,
        // nie-sekretne konwencje Keycloaka, więc tylko one dostają nieszkodliwy fallback.
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            throw new InvalidOperationException(
                "Keycloak:AdminUsername and Keycloak:AdminPassword must be configured — refusing to fall back to a default credential.");

        using var client = new HttpClient();
        var response = await client.PostAsync(
            $"{baseUrl}/realms/{realm}/protocol/openid-connect/token",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "password",
                ["client_id"] = clientId,
                ["username"] = username,
                ["password"] = password
            }),
            cancellationToken);

        var content = await response.Content.ReadFromJsonAsync<JsonElement>(
            cancellationToken: cancellationToken);

        return content.GetProperty("access_token").GetString()
            ?? throw new InvalidOperationException("Failed to get Keycloak admin token.");
    }
}
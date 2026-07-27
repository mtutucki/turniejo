namespace Turniejo.Auth.Infrastructure.Keycloak;

internal sealed record KeycloakUserRequest(
    string Username,
    string Email,
    string FirstName,
    string LastName,
    bool Enabled,
    bool EmailVerified,
    IReadOnlyList<KeycloakCredential> Credentials);

internal sealed record KeycloakCredential(
    string Type,
    string Value,
    bool Temporary);
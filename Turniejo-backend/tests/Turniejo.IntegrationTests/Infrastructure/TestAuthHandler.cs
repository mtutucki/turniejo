using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Turniejo.IntegrationTests.Infrastructure;

// Zastępuje prawdziwy JwtBearer (walidacja przeciw Keycloak JWKS) schematem sterowanym nagłówkiem —
// testy integracyjne mają weryfikować NASZ pipeline autoryzacji (AuthorizationBehavior +
// IAuthorizationService oparte o realne UserRoleAssignment w bazie), a nie samą walidację JWT.
// Brak nagłówka X-Test-UserId = żądanie nieuwierzytelnione (dokładnie jak brak/niepoprawny token).
public sealed class TestAuthHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "IntegrationTest";
    public const string UserIdHeader = "X-Test-UserId";
    public const string EmailHeader = "X-Test-Email";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(UserIdHeader, out var userIdValues) || string.IsNullOrWhiteSpace(userIdValues))
            return Task.FromResult(AuthenticateResult.NoResult());

        var userId = userIdValues.ToString();
        var email = Request.Headers.TryGetValue(EmailHeader, out var emailValues) && !string.IsNullOrWhiteSpace(emailValues)
            ? emailValues.ToString()
            : $"{userId}@integration.test";

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, userId),
            new Claim(ClaimTypes.Email, email)
        };
        var identity = new ClaimsIdentity(claims, SchemeName);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, SchemeName);

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}

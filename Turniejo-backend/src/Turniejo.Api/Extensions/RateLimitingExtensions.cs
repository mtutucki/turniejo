using System.Security.Claims;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

namespace Turniejo.Api.Extensions;

// Ochrona przed brute-force/spamem na endpointach bez roli/zasobu do zablokowania po stronie
// AuthorizationBehavior: kod dołączania do turnieju (do zgadnięcia) i reset hasła/rejestracja
// (email bombing, obciążenie Keycloak Admin API). Limity per identyfikator, nie globalne.
public static class RateLimitingExtensions
{
    public const string AuthAnonymous = "auth-anonymous";
    public const string JoinTournament = "join-tournament";
    public const string ChangePassword = "change-password";
    public const string Uploads = "uploads";

    public static IServiceCollection AddApiRateLimiting(this IServiceCollection services, IConfiguration configuration)
    {
        // Lokalnie (testy, zakładanie wielu kont z jednego IP) limity tylko przeszkadzają, więc da się je wyłączyć
        // przez RateLimiting:Enabled = false. Domyślnie są włączone, więc produkcja nie musi nic ustawiać.
        var enabled = configuration.GetValue("RateLimiting:Enabled", true);

        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            options.OnRejected = (context, cancellationToken) =>
            {
                context.HttpContext.Response.Headers.RetryAfter = "60";
                return ValueTask.CompletedTask;
            };

            // Rejestracja i reset hasła: przed zalogowaniem, więc jedyny dostępny identyfikator to IP.
            options.AddPolicy(AuthAnonymous, httpContext =>
                SlidingWindowOrNone(
                    enabled,
                    GetClientIp(httpContext),
                    _ => new SlidingWindowRateLimiterOptions
                    {
                        Window = TimeSpan.FromMinutes(5),
                        SegmentsPerWindow = 5,
                        PermitLimit = 5,
                        QueueLimit = 0,
                        QueueProcessingOrder = QueueProcessingOrder.OldestFirst
                    }));

            // Dołączanie kodem wymaga zalogowania (JoinTournamentCommand nie jest IPublicRequest),
            // więc limitujemy per user — nie da się obejść zmianą adresu IP.
            options.AddPolicy(JoinTournament, httpContext =>
                SlidingWindowOrNone(
                    enabled,
                    GetUserIdOrClientIp(httpContext),
                    _ => new SlidingWindowRateLimiterOptions
                    {
                        Window = TimeSpan.FromMinutes(10),
                        SegmentsPerWindow = 5,
                        PermitLimit = 10,
                        QueueLimit = 0,
                        QueueProcessingOrder = QueueProcessingOrder.OldestFirst
                    }));

            // Zmiana hasła sprawdza obecne hasło logowaniem do Keycloaka — kilka prób na kwadrans wystarczy
            // na literówki, a nie pozwala zgadywać.
            options.AddPolicy(ChangePassword, httpContext =>
                SlidingWindowOrNone(
                    enabled,
                    GetUserIdOrClientIp(httpContext),
                    _ => new SlidingWindowRateLimiterOptions
                    {
                        Window = TimeSpan.FromMinutes(15),
                        SegmentsPerWindow = 5,
                        PermitLimit = 5,
                        QueueLimit = 0,
                        QueueProcessingOrder = QueueProcessingOrder.OldestFirst
                    }));

            // Wgrywanie plików zajmuje dysk serwera. Na zmianę herbów kilkunastu drużyn wystarczy z zapasem.
            options.AddPolicy(Uploads, httpContext =>
                SlidingWindowOrNone(
                    enabled,
                    GetUserIdOrClientIp(httpContext),
                    _ => new SlidingWindowRateLimiterOptions
                    {
                        Window = TimeSpan.FromMinutes(10),
                        SegmentsPerWindow = 5,
                        PermitLimit = 30,
                        QueueLimit = 0,
                        QueueProcessingOrder = QueueProcessingOrder.OldestFirst
                    }));
        });

        return services;
    }

    private static RateLimitPartition<string> SlidingWindowOrNone(
        bool enabled,
        string partitionKey,
        Func<string, SlidingWindowRateLimiterOptions> factory) =>
        enabled
            ? RateLimitPartition.GetSlidingWindowLimiter(partitionKey, factory)
            : RateLimitPartition.GetNoLimiter(partitionKey);

    private static string GetClientIp(HttpContext httpContext) =>
        httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";

    private static string GetUserIdOrClientIp(HttpContext httpContext) =>
        httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? httpContext.User.FindFirstValue("sub")
            ?? GetClientIp(httpContext);
}

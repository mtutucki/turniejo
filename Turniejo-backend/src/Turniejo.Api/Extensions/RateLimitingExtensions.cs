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

    public static IServiceCollection AddApiRateLimiting(this IServiceCollection services)
    {
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
                RateLimitPartition.GetSlidingWindowLimiter(
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
                RateLimitPartition.GetSlidingWindowLimiter(
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
                RateLimitPartition.GetSlidingWindowLimiter(
                    GetUserIdOrClientIp(httpContext),
                    _ => new SlidingWindowRateLimiterOptions
                    {
                        Window = TimeSpan.FromMinutes(15),
                        SegmentsPerWindow = 5,
                        PermitLimit = 5,
                        QueueLimit = 0,
                        QueueProcessingOrder = QueueProcessingOrder.OldestFirst
                    }));
        });

        return services;
    }

    private static string GetClientIp(HttpContext httpContext) =>
        httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";

    private static string GetUserIdOrClientIp(HttpContext httpContext) =>
        httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? httpContext.User.FindFirstValue("sub")
            ?? GetClientIp(httpContext);
}

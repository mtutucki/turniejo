using System.Net;
using Microsoft.AspNetCore.HttpOverrides;

namespace Turniejo.Api.Extensions;

// Przeglądarka rozmawia z API przez frontend (rewrite /backend/* w Next.js — backend nie ma CORS), więc bez tego
// Connection.RemoteIpAddress to zawsze adres serwera Next i limity "per IP" z RateLimitingExtensions stają się
// jednym wspólnym limitem dla wszystkich użytkowników. Next przekazuje adres klienta w X-Forwarded-For —
// ufamy mu wyłącznie od znanych proxy (domyślnie loopback; w innych środowiskach ForwardedHeaders:KnownProxies
// albo ForwardedHeaders:KnownNetworks).
//
// Uwaga wdrożeniowa: Next.js ustawia X-Forwarded-For tylko, gdy żądanie go nie ma — jeśli frontend jest wystawiony
// bezpośrednio do internetu, klient może podać własną wartość. Przed Next musi stać reverse proxy, które nadpisuje
// ten nagłówek (np. nginx: proxy_set_header X-Forwarded-For $remote_addr).
public static class ForwardedHeadersExtensions
{
    public static IServiceCollection AddTrustedProxyForwarding(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor;
            // Tylko ostatni wpis — ten dopisany przez nasze proxy. Wcześniejsze pochodzą od klienta.
            options.ForwardLimit = 1;

            foreach (var proxy in configuration.GetSection("ForwardedHeaders:KnownProxies").Get<string[]>() ?? [])
            {
                if (IPAddress.TryParse(proxy, out var address))
                    options.KnownProxies.Add(address);
                else
                    throw new InvalidOperationException($"Invalid IP address in ForwardedHeaders:KnownProxies: '{proxy}'.");
            }

            // W Dockerze adresy kontenerów (Next, Caddy) są przydzielane dynamicznie — ufamy całej
            // wewnętrznej podsieci compose'a (CIDR), do której z zewnątrz nie da się podłączyć.
            foreach (var network in configuration.GetSection("ForwardedHeaders:KnownNetworks").Get<string[]>() ?? [])
            {
                if (Microsoft.AspNetCore.HttpOverrides.IPNetwork.TryParse(network, out var parsed))
                    options.KnownNetworks.Add(parsed);
                else
                    throw new InvalidOperationException($"Invalid CIDR in ForwardedHeaders:KnownNetworks: '{network}'.");
            }
        });

        return services;
    }
}

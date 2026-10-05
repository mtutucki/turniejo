using System.Net.Http.Headers;

namespace Turniejo.Auth.Infrastructure.Keycloak;

internal sealed class KeycloakAdminTokenHandler(KeycloakAdminTokenProvider tokenProvider)
    : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var token = await tokenProvider.GetAccessTokenAsync(cancellationToken);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await base.SendAsync(request, cancellationToken);
    }
}

using System.Security.Claims;
using Microsoft.AspNetCore.SignalR;

namespace Turniejo.Matches.Infrastructure.Realtime;

// Clients.User(userId) szuka połączeń po IUserIdProvider — ten sam identyfikator co ICurrentUserContext
// (claim "sub" z Keycloaka, albo NameIdentifier, gdy middleware zmapował go przy odczycie tokenu).
internal sealed class SubUserIdProvider : IUserIdProvider
{
    public string? GetUserId(HubConnectionContext connection) =>
        connection.User?.FindFirstValue(ClaimTypes.NameIdentifier) ?? connection.User?.FindFirstValue("sub");
}

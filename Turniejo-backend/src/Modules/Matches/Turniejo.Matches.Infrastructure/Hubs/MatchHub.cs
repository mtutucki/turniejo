using System.Security.Claims;
using Microsoft.AspNetCore.SignalR;
using Turniejo.Matches.Application.Services;

namespace Turniejo.Matches.Infrastructure.Hubs;

public sealed class MatchHub(RealtimeAccessGuard accessGuard) : Hub
{
    public async Task JoinMatchGroup(Guid matchId)
    {
        if (!await accessGuard.CanJoinMatchGroupAsync(matchId, GetUserId(), Context.ConnectionAborted))
            throw new HubException("Access denied.");

        await Groups.AddToGroupAsync(Context.ConnectionId, GroupName(matchId));
    }

    public Task LeaveMatchGroup(Guid matchId) =>
        Groups.RemoveFromGroupAsync(Context.ConnectionId, GroupName(matchId));

    public static string GroupName(Guid matchId) => $"matches/{matchId}";

    private Guid? GetUserId()
    {
        var value = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? Context.User?.FindFirstValue("sub");

        return Guid.TryParse(value, out var id) ? id : null;
    }
}

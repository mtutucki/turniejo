using Microsoft.AspNetCore.SignalR;

namespace Turniejo.Matches.Infrastructure.Hubs;

public sealed class MatchHub : Hub
{
    public Task JoinMatchGroup(Guid matchId) =>
        Groups.AddToGroupAsync(Context.ConnectionId, GroupName(matchId));

    public Task LeaveMatchGroup(Guid matchId) =>
        Groups.RemoveFromGroupAsync(Context.ConnectionId, GroupName(matchId));

    public static string GroupName(Guid matchId) => $"matches/{matchId}";
}

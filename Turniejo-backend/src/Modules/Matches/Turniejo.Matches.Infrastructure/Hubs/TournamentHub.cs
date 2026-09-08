using System.Security.Claims;
using Microsoft.AspNetCore.SignalR;
using Turniejo.Matches.Application.Services;

namespace Turniejo.Matches.Infrastructure.Hubs;

public sealed class TournamentHub(RealtimeAccessGuard accessGuard) : Hub
{
    public async Task JoinTournamentGroup(Guid tournamentId)
    {
        if (!await accessGuard.CanJoinTournamentGroupAsync(tournamentId, GetUserId(), Context.ConnectionAborted))
            throw new HubException("Access denied.");

        await Groups.AddToGroupAsync(Context.ConnectionId, GroupName(tournamentId));
    }

    public Task LeaveTournamentGroup(Guid tournamentId) =>
        Groups.RemoveFromGroupAsync(Context.ConnectionId, GroupName(tournamentId));

    public static string GroupName(Guid tournamentId) => $"tournaments/{tournamentId}";

    private Guid? GetUserId()
    {
        var value = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? Context.User?.FindFirstValue("sub");

        return Guid.TryParse(value, out var id) ? id : null;
    }
}

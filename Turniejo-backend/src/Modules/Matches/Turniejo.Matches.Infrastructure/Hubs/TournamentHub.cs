using Microsoft.AspNetCore.SignalR;

namespace Turniejo.Matches.Infrastructure.Hubs;

public sealed class TournamentHub : Hub
{
    public Task JoinTournamentGroup(Guid tournamentId) =>
        Groups.AddToGroupAsync(Context.ConnectionId, GroupName(tournamentId));

    public Task LeaveTournamentGroup(Guid tournamentId) =>
        Groups.RemoveFromGroupAsync(Context.ConnectionId, GroupName(tournamentId));

    public static string GroupName(Guid tournamentId) => $"tournaments/{tournamentId}";
}

using Microsoft.AspNetCore.SignalR;
using Turniejo.Matches.Infrastructure.Hubs;
using Turniejo.SharedKernel.Contracts;

namespace Turniejo.Matches.Infrastructure.Realtime;

internal sealed class SignalRRealtimeService(
    IHubContext<MatchHub> matchHubContext,
    IHubContext<TournamentHub> tournamentHubContext) : IRealtimeService
{
    public Task PublishMatchScoreChangedAsync(Guid matchId, int homeScore, int awayScore, CancellationToken cancellationToken = default) =>
        matchHubContext.Clients.Group(MatchHub.GroupName(matchId))
            .SendAsync("MatchScoreChanged", new { matchId, homeScore, awayScore }, cancellationToken);

    public Task PublishMatchStatusChangedAsync(Guid matchId, string status, CancellationToken cancellationToken = default) =>
        matchHubContext.Clients.Group(MatchHub.GroupName(matchId))
            .SendAsync("MatchStatusChanged", new { matchId, status }, cancellationToken);

    public Task PublishMatchEventAddedAsync(Guid matchId, Guid matchEventId, string type, CancellationToken cancellationToken = default) =>
        matchHubContext.Clients.Group(MatchHub.GroupName(matchId))
            .SendAsync("MatchEventAdded", new { matchId, matchEventId, type }, cancellationToken);

    public Task PublishStandingUpdatedAsync(Guid tournamentId, Guid groupId, CancellationToken cancellationToken = default) =>
        tournamentHubContext.Clients.Group(TournamentHub.GroupName(tournamentId))
            .SendAsync("StandingUpdated", new { tournamentId, groupId }, cancellationToken);

    public Task PublishBracketUpdatedAsync(Guid tournamentId, CancellationToken cancellationToken = default) =>
        tournamentHubContext.Clients.Group(TournamentHub.GroupName(tournamentId))
            .SendAsync("BracketUpdated", new { tournamentId }, cancellationToken);
}

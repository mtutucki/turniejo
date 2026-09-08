namespace Turniejo.SharedKernel.Contracts;

public interface IRealtimeService
{
    Task PublishMatchScoreChangedAsync(Guid matchId, int homeScore, int awayScore, CancellationToken cancellationToken = default);
    Task PublishMatchStatusChangedAsync(Guid matchId, string status, CancellationToken cancellationToken = default);
    Task PublishMatchEventAddedAsync(Guid matchId, Guid matchEventId, string type, CancellationToken cancellationToken = default);
    Task PublishStandingUpdatedAsync(Guid tournamentId, Guid groupId, CancellationToken cancellationToken = default);
    Task PublishBracketUpdatedAsync(Guid tournamentId, CancellationToken cancellationToken = default);
}
